from pathlib import Path
import subprocess
import time

path = Path('/opt/integration-hub/Caddyfile')
original = path.read_text()
if 'user.aurorasoftware.com' not in original:
    candidate = original + '''

# Aurora universal login and product workspace
user.aurorasoftware.com {
    @api path /api/* /connect/* /.well-known/* /health/*
    handle @api {
        reverse_proxy aurora-auth-api:8080
    }
    handle {
        reverse_proxy aurora-auth-web:80
    }
}
'''
    backup = path.with_name('Caddyfile.pre-aurora-' + time.strftime('%Y%m%d-%H%M%S'))
    backup.write_text(original)
    # Validate the complete candidate before replacing the shared proxy configuration.
    check = subprocess.run(['docker', 'exec', '-i', 'integration-hub-caddy-1', 'caddy', 'validate',
                            '--adapter', 'caddyfile', '--config', '/dev/stdin'],
                           input=candidate, text=True, capture_output=True)
    if check.returncode:
        raise RuntimeError(check.stderr)
    path.write_text(candidate)
    reload = subprocess.run(['docker', 'exec', 'integration-hub-caddy-1', 'caddy', 'reload',
                             '--adapter', 'caddyfile', '--config', '/etc/caddy/Caddyfile'],
                            text=True, capture_output=True)
    if reload.returncode:
        path.write_text(original)
        raise RuntimeError(reload.stderr)
    print('Proxy route added; backup: ' + str(backup))
else:
    print('Aurora proxy route already present.')
