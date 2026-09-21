"""Audit deployment and retire the task's temporary dashboard override. Print no secrets."""
import json
from pathlib import Path
import subprocess

names = ['aurora-auth-api', 'aurora-auth-web', 'aurora-auth-db', 'fo-staging-api', 'fo-staging-dashboard', 'fo-staging-daemon', 'fo-staging-db']
allowed = {'Aurora__Authority', 'Aurora__TenantId', 'VITE_API_HOST', 'VITE_AURORA_AUTHORITY', 'VITE_AURORA_CLIENT_ID', 'Hosting__PublicOrigin', 'Hosting__BehindReverseProxy'}
for name in names:
    item = json.loads(subprocess.check_output(['docker', 'inspect', name]))[0]
    print(name, item['State']['Status'], item['Config']['Image'], item['State'].get('Health', {}).get('Status', ''))
    print({k: v for line in item['Config'].get('Env', []) for k, v in [line.split('=', 1)] if k in allowed})
query = '''SELECT "UserName", "AuroraUserId" FROM "AspNetUsers" WHERE "NormalizedUserName"='BBATTS';
SELECT "MigrationId" FROM "__EFMigrationsHistory" WHERE "MigrationId" LIKE '%AuroraUserLink%';'''
subprocess.run(['docker', 'exec', '-i', 'fo-staging-db', 'psql', '-U', 'postgres', '-d', 'fo_auth', '-v', 'ON_ERROR_STOP=1'], input=query, text=True, check=True)
for path in [Path('/opt/aurora-auth/private'), Path('/opt/aurora-auth/private/api.env'), Path('/opt/aurora-auth/private/bootstrap.json')]:
    print(path, oct(path.stat().st_mode & 0o777))
override = Path('/opt/freightops/FreightOps/docker-compose.aurora.yml')
if override.exists():
    override.rename(override.with_name('docker-compose.aurora.yml.retired-20260913'))
    print('Retired the temporary dashboard override; standard staging compose is active.')
