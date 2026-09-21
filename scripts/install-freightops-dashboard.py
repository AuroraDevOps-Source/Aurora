from pathlib import Path
import json
import subprocess
import time

root = Path('/opt/freightops/FreightOps')
release = Path('/opt/aurora-auth/releases/20260913-1/freightops-dashboard')
subprocess.run(['tar', '-xzf', str(release / 'release.tar.gz'), '-C', str(release)], check=True)
image = 'freightops-dashboard-aurora:20260913-1'
subprocess.run(['docker', 'build', '--progress=plain', '-t', image, str(release)], check=True)
env = root / '.env'
before = env.read_text()
backup = root / ('.env.pre-aurora-dashboard-' + time.strftime('%Y%m%d-%H%M%S'))
backup.write_text(before)
backup.chmod(0o600)
desired = {
    'VITE_AURORA_AUTHORITY': 'https://user.aurorasoftware.com',
    'VITE_AURORA_CLIENT_ID': 'freightops-spa',
}
lines = [line for line in before.splitlines() if line.split('=', 1)[0] not in desired]
env.write_text('\n'.join(lines) + '\n' + ''.join(f'{k}={v}\n' for k, v in desired.items()))
override = root / 'docker-compose.aurora.yml'
override.write_text('''services:
  dashboard:
    image: freightops-dashboard-aurora:20260913-1
    environment:
      VITE_AURORA_AUTHORITY: ${VITE_AURORA_AUTHORITY}
      VITE_AURORA_CLIENT_ID: ${VITE_AURORA_CLIENT_ID}
''')
subprocess.run(['docker', 'compose', '--ansi', 'never', '-p', 'freightops',
    '-f', 'docker-compose.staging.yml', '-f', 'docker-compose.aurora.yml',
    'up', '-d', '--no-deps', 'dashboard'], cwd=root, check=True)
print('Corrected FreightOps dashboard deployed; server override: ' + str(override))
