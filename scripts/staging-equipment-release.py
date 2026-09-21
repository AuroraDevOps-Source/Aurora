"""Staging-only additive equipment migration and Aurora API/web release."""
import argparse
import hashlib
from pathlib import Path
import re
import subprocess
import tarfile
import time

parser = argparse.ArgumentParser()
parser.add_argument('release')
parser.add_argument('sha256')
args = parser.parse_args()
if not re.fullmatch(r'\d{8}-\d+', args.release):
    raise ValueError('Invalid release ID')
root = Path('/opt/aurora-auth')
archive = root / ('aurora-release-' + args.release + '.tar.gz')
if hashlib.sha256(archive.read_bytes()).hexdigest().lower() != args.sha256.lower():
    raise RuntimeError('Release archive checksum mismatch')
source = root / 'releases' / args.release
if source.exists():
    raise RuntimeError('Release directory already exists; inspect before retrying')
source.mkdir()
with tarfile.open(archive) as package:
    for member in package.getmembers():
        target = (source / member.name).resolve()
        if not target.is_relative_to(source.resolve()) or not (member.isfile() or member.isdir()):
            raise RuntimeError('Unsafe archive member')
    # Python 3.11.2 on staging predates extraction filters; members are checked above.
    package.extractall(source)

stamp = time.strftime('%Y%m%d-%H%M%S')
backup = root / 'private' / ('equipment-pre-' + args.release + '-' + stamp + '.dump')
with backup.open('xb') as output:
    backup.chmod(0o600)
    subprocess.run(['docker', 'exec', 'aurora-auth-db', 'pg_dump', '-U', 'postgres', '-d', 'aurora', '-Fc'], stdout=output, check=True)
if backup.stat().st_size < 100:
    raise RuntimeError('Database backup is empty')
print('Database backup created:', backup.name, flush=True)
subprocess.run(['docker', 'run', '--rm', '--network', 'aurora-auth_default',
    '--env-file', str(root / 'private/migrations.env'), '-e', 'Seed__DevData=false',
    '-v', f'{source}/migrations:/app:ro', '-w', '/app',
    'mcr.microsoft.com/dotnet/aspnet:10.0', 'dotnet', 'Aurora.Migrations.dll'], check=True)
subprocess.run(['python3', str(source / 'scripts/deploy-aurora-release.py'), args.release, '--embedded'], check=True)
print('Equipment migration and test-server release completed.', flush=True)
