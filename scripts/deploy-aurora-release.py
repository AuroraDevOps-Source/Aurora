"""Update only the Aurora API/web containers. Never runs migrations or tenant seeding."""
import argparse
from pathlib import Path
import re
import subprocess
import time
import urllib.request
import urllib.error

parser = argparse.ArgumentParser()
parser.add_argument('release')
parser.add_argument('--embedded', action='store_true')
args = parser.parse_args()
if not re.fullmatch(r'[0-9]{8}-[0-9]+', args.release):
    raise ValueError('Release must be YYYYMMDD-N')
root = Path('/opt/aurora-auth')
source = root / 'releases' / args.release
if not (source / 'api/Aurora.Api.dll').is_file() or not (source / 'client/wwwroot/index.html').is_file():
    raise ValueError('Release is incomplete')
for name in ('api', 'web'):
    subprocess.run(['docker', 'build', '-q', '-f', str(source / f'deploy/Dockerfile.{name}'),
        '-t', f'aurora-auth-{name}:{args.release}', str(source)], check=True)
stamp = time.strftime('%Y%m%d-%H%M%S')
for path in (root / '.env', root / 'private/api.env'):
    backup = path.with_name(path.name + '.pre-' + args.release + '-' + stamp)
    backup.write_bytes(path.read_bytes())
    backup.chmod(0o600)
    key, value = ('RELEASE_TAG', args.release) if path.name == '.env' else ('Auth__AllowEmbeddedLogin', str(args.embedded).lower())
    lines = [line for line in path.read_text().splitlines() if line.split('=', 1)[0] != key]
    path.write_text('\n'.join(lines) + f'\n{key}={value}\n')
    path.chmod(0o600)
subprocess.run(['docker', 'compose', '--ansi', 'never', '-p', 'aurora-auth', '-f', str(root / 'compose.yml'),
    'up', '-d', '--no-deps', 'api', 'web'], cwd=root, check=True)
for attempt in range(15):
    try:
        with urllib.request.urlopen(urllib.request.Request('https://user.aurorasoftware.com/health/ready',
                headers={'User-Agent': 'Mozilla/5.0 AuroraStagingVerification/1.0'}), timeout=5) as response:
            if response.status == 200:
                break
    except (urllib.error.URLError, TimeoutError):
        pass
    time.sleep(2)
else:
    raise RuntimeError('The deployed Aurora API did not become ready')
print('Deployed Aurora ' + args.release + '; databases and product deployments unchanged.')
