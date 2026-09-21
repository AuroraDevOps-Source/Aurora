"""Run on the staging host after uploading a validated Aurora release."""
import json
import os
from pathlib import Path
import secrets
import subprocess
import time

ROOT = Path('/opt/aurora-auth')
RELEASE = '20260914-5'
SOURCE = ROOT / 'releases' / RELEASE
PRIVATE = ROOT / 'private'
PRIVATE.mkdir(parents=True, exist_ok=True, mode=0o700)
os.umask(0o077)

def run(*args, **kwargs):
    return subprocess.run(args, check=True, text=True, **kwargs)

def output(*args):
    return subprocess.check_output(args, text=True)

def write_env(path, values):
    path.write_text(''.join(f'{k}={v}\n' for k, v in values.items()))
    path.chmod(0o600)

state_path = PRIVATE / 'bootstrap.json'
if state_path.exists():
    state = json.loads(state_path.read_text())
else:
    state = {k: secrets.token_hex(24) for k in ('postgres', 'owner', 'app', 'certificate', 'admin_password')}
    state['admin_password'] = 'Aa1!' + state['admin_password']
    state['admin_username'] = 'admin@aurora.local'
    state_path.write_text(json.dumps(state))

write_env(PRIVATE / 'db.env', {'POSTGRES_PASSWORD': state['postgres'], 'POSTGRES_DB': 'postgres'})
ptv_env = json.loads(output('docker', 'inspect', 'ptv-route-desk-web-1'))[0]['Config']['Env']
ptv = dict(item.split('=', 1) for item in ptv_env if item.startswith('Ptv__'))
if not ptv.get('Ptv__ApiKey'):
    raise RuntimeError('PTV key missing from the existing staging Route Desk')

origins = ['https://user.aurorasoftware.com', 'https://freightops.aurorasoftware.net', 'https://integration.novafreightops.com']
common = {
    'DOTNET_ENVIRONMENT': 'Staging',
    'ASPNETCORE_ENVIRONMENT': 'Staging',
    'Hosting__PublicOrigin': origins[0],
    'Hosting__BehindReverseProxy': 'true',
    'Products__AuroraUrl': origins[0],
    'Products__FreightOpsUrl': origins[1],
    'Products__HubUrl': origins[2],
}
api_env = {
    **common,
    'ConnectionStrings__Aurora': f"Host=db;Database=aurora;Username=aurora_app;Password={state['app']}",
    'Hosting__DataProtectionPath': '/keys',
    'Auth__SigningCertificatePath': '/certificates/signing.pfx',
    'Auth__EncryptionCertificatePath': '/certificates/encryption.pfx',
    'Auth__CertificatePassword': state['certificate'],
    'Auth__AllowEmbeddedLogin': 'true',
    'Logging__LogLevel__Default': 'Warning',
    **{f'Cors__AllowedOrigins__{i}': origin for i, origin in enumerate(origins)},
    **ptv,
}
write_env(PRIVATE / 'api.env', api_env)
write_env(PRIVATE / 'migrations.env', {
    **common,
    'ConnectionStrings__Aurora': f"Host=db;Database=aurora;Username=aurora_owner;Password={state['owner']}",
    'Seed__DevData': 'true',
    'Seed__AdminEmail': state.get('admin_email', state['admin_username']),
    'Seed__AdminPassword': state['admin_password'],
    'Seed__AdminUserId': '01a08138-7cb9-7a10-8bd8-450a7bfd755e',
})

cert_dir = PRIVATE / 'certificates'
cert_dir.mkdir(exist_ok=True, mode=0o700)
for name in ('signing', 'encryption'):
    if not (cert_dir / f'{name}.pfx').exists():
        run('openssl', 'req', '-x509', '-newkey', 'rsa:3072', '-nodes', '-days', '730',
            '-subj', f'/CN=Aurora Staging {name}', '-keyout', str(cert_dir / f'{name}.key'),
            '-out', str(cert_dir / f'{name}.crt'), stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        cert_env = os.environ | {'AURORA_CERT_PASSWORD': state['certificate']}
        run('openssl', 'pkcs12', '-export', '-inkey', str(cert_dir / f'{name}.key'),
            '-in', str(cert_dir / f'{name}.crt'), '-out', str(cert_dir / f'{name}.pfx'),
            '-passout', 'env:AURORA_CERT_PASSWORD', env=cert_env)

(ROOT / 'compose.yml').write_text((SOURCE / 'deploy/compose.yml').read_text())
write_env(ROOT / '.env', {'RELEASE_TAG': RELEASE})
compose = ['docker', 'compose', '--ansi', 'never', '-p', 'aurora-auth', '-f', str(ROOT / 'compose.yml')]
run(*compose, 'up', '-d', 'db', cwd=ROOT)
for _ in range(40):
    if output('docker', 'inspect', 'aurora-auth-db', '--format', '{{.State.Health.Status}}').strip() == 'healthy':
        break
    time.sleep(2)
else:
    raise RuntimeError('Aurora database did not become healthy')

exists = output('docker', 'exec', 'aurora-auth-db', 'psql', '-U', 'postgres', '-Atc',
                "SELECT count(*) FROM pg_database WHERE datname='aurora'").strip()
if exists == '0':
    sql = f"""CREATE ROLE aurora_owner LOGIN BYPASSRLS PASSWORD '{state['owner']}';
CREATE ROLE aurora_app LOGIN PASSWORD '{state['app']}';
CREATE ROLE aurora_admin NOLOGIN BYPASSRLS;
CREATE DATABASE aurora OWNER aurora_owner;
"""
    run('docker', 'exec', '-i', 'aurora-auth-db', 'psql', '-U', 'postgres', '-v', 'ON_ERROR_STOP=1', input=sql)

run('docker', 'run', '--rm', '--network', 'aurora-auth_default',
    '--env-file', str(PRIVATE / 'migrations.env'), '-v', f'{SOURCE}/migrations:/app:ro', '-w', '/app',
    'mcr.microsoft.com/dotnet/aspnet:10.0', 'dotnet', 'Aurora.Migrations.dll')
for name in ('api', 'web'):
    run('docker', 'build', '--progress=plain', '-f', str(SOURCE / f'deploy/Dockerfile.{name}'),
        '-t', f'aurora-auth-{name}:{RELEASE}', str(SOURCE))
run(*compose, 'up', '-d', '--no-deps', 'api', 'web', cwd=ROOT)
print('Aurora installed with persistent database, certificates, and data-protection keys.')
