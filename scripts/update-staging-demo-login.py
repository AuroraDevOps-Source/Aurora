"""Run manually on staging after the explicit one-account demo login request.

Reads credentials from a root-only input file, never source or command arguments.
Uses Identity via the scoped maintenance tool; does not migrate or seed data.
"""
import json
import os
from pathlib import Path
import subprocess
import sys
import time

root = Path('/opt/aurora-auth')
private = root / 'private'
if sys.argv[1:] != ['--apply-approved-demo-login']:
    raise SystemExit('Explicit approved-demo mode is required.')
os.umask(0o077)
input_path = private / 'demo-login-change.json'
if input_path.stat().st_mode & 0o077:
    raise RuntimeError('Login input permissions must be root-only.')
login = json.loads(input_path.read_text())
if login.get('UserName') != 'bbatts' or len(login.get('Password', '')) != 6 or not login['Password'].isascii() or not login['Password'].isdigit():
    raise RuntimeError('Unexpected demo login input.')
state_path = private / 'bootstrap.json'
state = json.loads(state_path.read_text())
if state['admin_username'] != 'admin@aurora.local':
    raise RuntimeError('Bootstrap username changed; inspect before retrying.')
stamp = time.strftime('%Y%m%d-%H%M%S')
for path in (state_path, private / 'migrations.env'):
    backup = path.with_name(path.name + '.pre-demo-login-' + stamp)
    with backup.open('xb') as output:
        output.write(path.read_bytes())
    backup.chmod(0o600)

subprocess.run([
    'docker', 'run', '--rm', '-i', '--network', 'aurora-auth_default',
    '--env-file', str(private / 'migrations.env'),
    '-v', f'{root}/maintenance/staging-account:/app:ro',
    '-v', f'{private}:/task-private', '-w', '/app',
    'mcr.microsoft.com/dotnet/aspnet:10.0', 'dotnet', 'StagingAccount.dll',
    '--apply-approved-demo-login'
], input=json.dumps(login), text=True, check=True)

state['admin_email'] = state.get('admin_email', state['admin_username'])
state['admin_username'] = login['UserName']
state['admin_password'] = login['Password']
temporary = state_path.with_name('bootstrap.demo-login.pending.json')
temporary.write_text(json.dumps(state))
temporary.chmod(0o600)
temporary.replace(state_path)
# Keep the original email so a future explicit seed finds this same account.
env_path = private / 'migrations.env'
env = dict(line.split('=', 1) for line in env_path.read_text().splitlines() if '=' in line)
env['Seed__AdminEmail'] = state['admin_email']
env['Seed__AdminPassword'] = state['admin_password']
temporary = env_path.with_name('migrations.demo-login.pending.env')
temporary.write_text(''.join(f'{key}={value}\n' for key, value in env.items()))
temporary.chmod(0o600)
temporary.replace(env_path)
print('Protected bootstrap login and future seed email are synchronized; no API password policy was changed.')
