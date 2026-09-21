"""Task-local staging SSH helper; credentials remain in the operations document."""
import argparse
import pathlib
import re
import sys

import paramiko

parser = argparse.ArgumentParser()
parser.add_argument("command", nargs="?")
parser.add_argument("--script")
parser.add_argument("--put", nargs=2, metavar=("LOCAL", "REMOTE"))
parser.add_argument("--export-login")
args = parser.parse_args()

profile = pathlib.Path("C:/Users/Bryan")
reference = (profile / "OneDrive - Aurora Software/Documentation/TEST-STAGING.md").read_text(encoding="utf-8-sig")
match = re.search(r"\| Root password \| `([^`]+)`", reference)
if not match:
    raise RuntimeError("Staging password unavailable in operations reference")
client = paramiko.SSHClient()
client.load_host_keys(str(profile / ".ssh/known_hosts_freightops"))
client.set_missing_host_key_policy(paramiko.RejectPolicy())
client.connect("172.23.0.13", port=61223, username="root", password=match.group(1),
               look_for_keys=False, allow_agent=False, timeout=15)
try:
    if args.export_login:
        import json
        with client.open_sftp() as sftp:
            with sftp.open('/opt/aurora-auth/private/bootstrap.json') as source:
                state = json.load(source)
        target = pathlib.Path(args.export_login)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text('URL: https://user.aurorasoftware.com\nUsername: ' + state['admin_username'] + '\nPassword: ' + state['admin_password'] + '\n', encoding='utf-8')
        print('Staging login saved to ' + str(target))
    elif args.put:
        with client.open_sftp() as sftp:
            sftp.put(*args.put)
        print("Uploaded " + args.put[1])
    else:
        command = args.command if not args.script else "bash -se"
        stdin, stdout, stderr = client.exec_command(command, timeout=600)
        if args.script:
            stdin.write(pathlib.Path(args.script).read_text(encoding="utf-8-sig"))
        stdin.channel.shutdown_write()
        # The commands used here have bounded output; retain the actual exit code.
        sys.stdout.buffer.write(stdout.read())
        sys.stderr.buffer.write(stderr.read())
        sys.exit(stdout.channel.recv_exit_status())
finally:
    client.close()
