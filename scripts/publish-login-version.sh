set -eu
cd /opt/aurora-auth
printf '%s\n' 'a3298485e9e4633cd2b97ef60691be3666769c7dbc29b0c5c08155d112fc1f7e  aurora-release-20260915-2.tar.gz' | sha256sum -c -
test ! -e releases/20260915-2
mkdir releases/20260915-2
tar -xzf aurora-release-20260915-2.tar.gz -C releases/20260915-2
python3 releases/20260915-2/scripts/deploy-aurora-release.py 20260915-2 --embedded
python3 releases/20260915-1/scripts/verify-sso.py --embedded
