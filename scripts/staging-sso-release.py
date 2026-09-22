"""Publish the reviewed Aurora/TMS working-tree releases to the named test installs only.

Run on staging. Credentials stay in existing server files. Prepare creates protected backups,
verifies archives and builds runtime images; activate applies additive schema/config changes.
"""
import argparse, hashlib, json, re, shutil, subprocess, tarfile, time
from pathlib import Path

p=argparse.ArgumentParser()
p.add_argument('mode', choices=['prepare','activate'])
p.add_argument('release')
p.add_argument('aurora_sha')
p.add_argument('tms_sha')
a=p.parse_args()
if not re.fullmatch(r'\d{8}-\d+', a.release): raise ValueError('Invalid release')
if subprocess.check_output(['hostname'],text=True).strip() != 'FreightOps-Daemon-01': raise RuntimeError('Wrong host')
auth=Path('/opt/aurora-auth'); tms=Path('/opt/aurora-nova')
def run(*cmd, **kw): return subprocess.run(cmd, check=True, **kw)
def output(*cmd): return subprocess.check_output(cmd,text=True).strip()
def sql(container,user,statement):
    return output('docker','exec',container,'psql','-v','ON_ERROR_STOP=1','-U',user,'-d','aurora','-Atc',statement)
def backup_dir(root): return root/'backups'/('sso-'+a.release)
def source(root): return root/'releases'/a.release

if a.mode == 'prepare':
    for root,prefix,sha,db,user in [(auth,'aurora',a.aurora_sha,'aurora-auth-db','postgres'),(tms,'auroratms',a.tms_sha,'aurora-nova-db','aurora')]:
        package=root/(prefix+'-release-'+a.release+'.tar.gz')
        if hashlib.sha256(package.read_bytes()).hexdigest()!=sha: raise RuntimeError('Archive checksum mismatch: '+prefix)
        dest=source(root)
        dest.mkdir(parents=True,exist_ok=False)
        with tarfile.open(package) as tar:
            for member in tar.getmembers():
                if not (dest/member.name).resolve().is_relative_to(dest.resolve()) or not(member.isfile() or member.isdir()): raise RuntimeError('Unsafe archive')
            tar.extractall(dest)
        backup=backup_dir(root); backup.mkdir(parents=True,mode=0o700,exist_ok=False)
        files=['.env','compose.yml','private/api.env','private/migrations.env'] if root==auth else ['.env','docker-compose.server.yml']
        for name in files:
            target=backup/name; target.parent.mkdir(parents=True,exist_ok=True,mode=0o700)
            shutil.copy2(root/name,target); target.chmod(0o600)
        dump=backup/'database.dump'
        with dump.open('xb') as stream:
            dump.chmod(0o600)
            run('docker','exec',db,'pg_dump','-U',user,'-d','aurora','-Fc',stdout=stream)
        with dump.open('rb') as stream:
            run('docker','exec','-i',db,'pg_restore','--list',stdin=stream,stdout=subprocess.DEVNULL)
        names=['aurora-auth-api','aurora-auth-web'] if root==auth else ['aurora-nova-api','aurora-nova-web']
        images={}
        for name in names:
            info=json.loads(output('docker','inspect',name))[0]
            images[name]=info['Image']
            run('docker','tag',info['Image'],name+':rollback-'+a.release)
        (backup/'images.json').write_text(json.dumps(images,indent=2))
        for name in ('api','web'):
            dockerfile=dest/('deploy/Dockerfile.'+name if root==auth else 'Dockerfile.'+name)
            tag=('aurora-auth-' if root==auth else 'aurora-nova-')+name+':'+a.release
            run('docker','build','-q','-f',str(dockerfile),'-t',tag,str(dest))
        print('Prepared',prefix,'backup:',backup,flush=True)
    raise SystemExit(0)

for root in (auth,tms):
    if not (backup_dir(root)/'database.dump').is_file(): raise RuntimeError('Missing verified backup')

# Product/role schema only; never run the development tenant/account seeder.
run('docker','run','--rm','--network','aurora-auth_default','--env-file',str(auth/'private/migrations.env'),
    '-e','Seed__DevData=false','-v',str(source(auth)/'migrations')+':/app:ro','-w','/app',
    'mcr.microsoft.com/dotnet/aspnet:10.0','dotnet','Aurora.Migrations.dll')

print(sql('aurora-auth-db','postgres', '''BEGIN;
DO $$ BEGIN
IF NOT EXISTS (SELECT 1 FROM tenant WHERE id='11111111-1111-1111-1111-111111111111' AND name='Aurora Demo Co') THEN RAISE EXCEPTION 'Wrong Aurora tenant'; END IF;
IF NOT EXISTS (SELECT 1 FROM "AspNetUsers" WHERE "Id"='01a08138-7cb9-7a10-8bd8-450a7bfd755e' AND "UserName"='bbatts') THEN RAISE EXCEPTION 'Wrong Aurora account'; END IF;
END $$;
INSERT INTO "OpenIddictApplications" ("Id","ApplicationType","ClientId","ClientType","ConsentType","DisplayName","Permissions","Requirements","RedirectUris","PostLogoutRedirectUris","ConcurrencyToken")
SELECT gen_random_uuid(),"ApplicationType",'auroratms-spa',"ClientType","ConsentType",'Aurora TMS',"Permissions","Requirements",'["https://nova.aurorasoftware.net/auth/callback"]','["https://nova.aurorasoftware.net/auth/logout-callback"]',gen_random_uuid()::text
FROM "OpenIddictApplications" WHERE "ClientId"='freightops-spa'
ON CONFLICT ("ClientId") DO UPDATE SET "RedirectUris"=EXCLUDED."RedirectUris", "Requirements"=EXCLUDED."Requirements";
DO $$ BEGIN
IF NOT EXISTS (SELECT 1 FROM "OpenIddictApplications" WHERE "ClientId"='auroratms-spa' AND "ClientType"='public') THEN RAISE EXCEPTION 'TMS client registration failed'; END IF;
END $$;
INSERT INTO tenant_product (tenant_id,product_code,instance_url,is_active)
VALUES ('11111111-1111-1111-1111-111111111111','auroratms','https://nova.aurorasoftware.net/auth/sso',true)
ON CONFLICT (tenant_id,product_code) DO UPDATE SET instance_url=EXCLUDED.instance_url,is_active=true;
INSERT INTO "AspNetUserRoles" ("TenantId","UserId","RoleId")
SELECT '11111111-1111-1111-1111-111111111111','01a08138-7cb9-7a10-8bd8-450a7bfd755e',"Id" FROM "AspNetRoles" WHERE "Name"='atms:Admin'
ON CONFLICT DO NOTHING;
COMMIT;'''))

# Retain all existing origins; add only the deployed TMS origin.
path=auth/'private/api.env'; lines=path.read_text().splitlines()
if not any(line.endswith('=https://nova.aurorasoftware.net') and line.startswith('Cors__AllowedOrigins__') for line in lines):
    indices=[int(line.split('=',1)[0].rsplit('__',1)[1]) for line in lines if line.startswith('Cors__AllowedOrigins__')]
    lines.append('Cors__AllowedOrigins__'+str(max(indices,default=-1)+1)+'=https://nova.aurorasoftware.net')
path.write_text('\n'.join(lines)+'\n');path.chmod(0o600)
run('python3',str(source(auth)/'scripts/deploy-aurora-release.py'),a.release,'--embedded')

# Persist image pins and SSO configuration in the install's existing compose file.
path=tms/'docker-compose.server.yml'; body=path.read_text()
if '  api:\n    build: ./backend\n' not in body or '  web:\n    build: ./frontend\n' not in body: raise RuntimeError('Unexpected TMS compose layout')
body=body.replace('  api:\n    build: ./backend\n','  api:\n    build: ./backend\n    image: aurora-nova-api:'+a.release+'\n',1)
body=body.replace('  web:\n    build: ./frontend\n','  web:\n    build: ./frontend\n    image: aurora-nova-web:'+a.release+'\n',1)
body=body.replace('      ASPNETCORE_ENVIRONMENT: Production','      ASPNETCORE_ENVIRONMENT: Production\n      Aurora__Authority: https://user.aurorasoftware.com\n      Aurora__Audience: auroratms-api\n      Aurora__ClientId: auroratms-spa\n      Aurora__RequireHttpsMetadata: "true"',1)
body=body.replace('      Seed__Enabled: "true"','      Seed__Enabled: "false"',1)
path.write_text(body)
run('docker','compose','-p','aurora-nova','-f',str(path),'up','-d','--no-deps','--no-build','api','web',cwd=tms)
for attempt in range(40):
    columns=sql('aurora-nova-db','aurora',"SELECT count(*) FROM information_schema.columns WHERE table_name='AspNetUsers' AND column_name='aurora_user_id'")
    roles=sql('aurora-nova-db','aurora',"SELECT count(*) FROM information_schema.columns WHERE table_name='refresh_tokens' AND column_name='aurora_roles'")
    if columns=='1' and roles=='1': break
    time.sleep(2)
else: raise RuntimeError('TMS additive migrations did not complete')
print(sql('aurora-nova-db','aurora', '''BEGIN;
DO $$ BEGIN
IF NOT EXISTS (SELECT 1 FROM tenants WHERE id='53b445f7-71b6-4d65-a3c0-e8c5648eea09' AND slug='demo' AND is_active AND (aurora_tenant_id IS NULL OR aurora_tenant_id='11111111-1111-1111-1111-111111111111')) THEN RAISE EXCEPTION 'Wrong TMS workspace or conflicting link'; END IF;
IF NOT EXISTS (SELECT 1 FROM "AspNetUsers" u JOIN "AspNetUserRoles" ur ON ur.user_id=u.id JOIN "AspNetRoles" r ON r.id=ur.role_id WHERE u.id='9f21cec7-d066-4025-993c-321c5bbfa772' AND u.email='bryan.batts@aurorasoftware.com' AND u.tenant_id='53b445f7-71b6-4d65-a3c0-e8c5648eea09' AND u.is_active AND r.name='Admin' AND (u.aurora_user_id IS NULL OR u.aurora_user_id='01a08138-7cb9-7a10-8bd8-450a7bfd755e')) THEN RAISE EXCEPTION 'Wrong TMS user or conflicting link'; END IF;
END $$;
UPDATE tenants SET aurora_tenant_id='11111111-1111-1111-1111-111111111111' WHERE id='53b445f7-71b6-4d65-a3c0-e8c5648eea09';
UPDATE "AspNetUsers" SET aurora_user_id='01a08138-7cb9-7a10-8bd8-450a7bfd755e' WHERE id='9f21cec7-d066-4025-993c-321c5bbfa772';
COMMIT;'''))
print('Both test installations activated; verify health and sign-in before declaring complete.',flush=True)
