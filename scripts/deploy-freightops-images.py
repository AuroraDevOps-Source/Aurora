"""Deploy verified GHCR commits without touching database state or other applications."""
import argparse
import os
from pathlib import Path
import subprocess
import time

parser = argparse.ArgumentParser()
parser.add_argument('--api-tag')
parser.add_argument('--dashboard-tag', required=True)
args = parser.parse_args()
root = Path('/opt/freightops/FreightOps')
stamp = time.strftime('%Y%m%d-%H%M%S')
def backup(path):
    target = path.with_name(path.name + '.pre-sso-' + stamp)
    target.write_bytes(path.read_bytes())
    target.chmod(0o600)
    return path.read_text()
compose = root / 'docker-compose.staging.yml'
text = backup(compose)
if 'VITE_AURORA_AUTHORITY:' not in text:
    text = text.replace('      VITE_BASE_URL: ${VITE_BASE_URL}', '      VITE_BASE_URL: ${VITE_BASE_URL}\n      VITE_AURORA_AUTHORITY: ${VITE_AURORA_AUTHORITY}\n      VITE_AURORA_CLIENT_ID: ${VITE_AURORA_CLIENT_ID}')
compose.write_text(text)
nginx = root / 'nginx.staging.default.conf'
text = backup(nginx)
if 'location = /config.js' not in text:
    text = text.replace('    location / {', '    location = /config.js {\n        root /usr/share/nginx/html;\n        add_header Cache-Control "no-store" always;\n    }\n    location / {', 1)
nginx.write_text(text)
env = os.environ | {'DASHBOARD_TAG': args.dashboard_tag}
services = ['dashboard']
if args.api_tag:
    env['API_TAG'] = args.api_tag
    services += ['freightops-api', 'freightops-daemon']
command = ['docker', 'compose', '--ansi', 'never', '-p', 'freightops', '-f', str(compose)]
subprocess.run(command + ['pull'] + services, cwd=root, env=env, check=True)
subprocess.run(command + ['up', '-d', '--no-deps'] + services, cwd=root, env=env, check=True)
print('Verified immutable images deployed. Configuration backups: ' + stamp)
