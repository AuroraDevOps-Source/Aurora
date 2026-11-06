"""Exercise local Aurora password and OIDC sign-in with isolated tenant fixtures."""
import base64, hashlib, http.cookiejar, json, os, secrets, ssl, struct, subprocess, urllib.error, urllib.parse, urllib.request, uuid

origin = 'https://localhost:7077'
callback = 'https://localhost:7259/authentication/login-callback'
user, home, other = [str(uuid.uuid4()) for _ in range(3)]
username = 'selection-' + user
password = secrets.token_urlsafe(32)
salt = secrets.token_bytes(16)
digest = hashlib.pbkdf2_hmac('sha512',password.encode(),salt,100000)
encoded = base64.b64encode(b'\x01'+struct.pack('>III',2,100000,16)+salt+digest).decode()
def sql(query):
    result = subprocess.run([os.environ.get('PSQL','psql'),'-XAt','-h','127.0.0.1','-p','55432','-U','postgres','-d','aurora_test','-v','ON_ERROR_STOP=1'],input=query,text=True,check=True,capture_output=True)
    return result.stdout.strip()
class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self,*args): return None
browser = urllib.request.build_opener(urllib.request.HTTPSHandler(context=ssl._create_unverified_context()),urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()),NoRedirect())
def request(path,data=None,headers=None):
    try: return browser.open(urllib.request.Request(origin+path,data=data,headers=headers or {}),timeout=20)
    except urllib.error.HTTPError as exc: return exc
def login(selected=None):
    body=dict(userName=username,password=password,tenantId=selected)
    return request('/api/auth/login',json.dumps(body).encode(),{'Content-Type':'application/json'})
def token():
    verifier=secrets.token_urlsafe(40)
    query=dict(client_id='aurora-spa',redirect_uri=callback,response_type='code',scope='openid profile roles',state='state',code_challenge=base64.urlsafe_b64encode(hashlib.sha256(verifier.encode()).digest()).decode().rstrip('='),code_challenge_method='S256')
    response=request('/connect/authorize?'+urllib.parse.urlencode(query))
    params=urllib.parse.parse_qs(urllib.parse.urlsplit(response.headers.get('Location','')).query)
    if 'code' not in params: return None
    body=urllib.parse.urlencode(dict(grant_type='authorization_code',client_id='aurora-spa',code=params['code'][0],redirect_uri=callback,code_verifier=verifier)).encode()
    return json.load(request('/connect/token',body,{'Content-Type':'application/x-www-form-urlencoded'}))['access_token']
def company(access):
    return request('/api/v1/me/tenant',headers={'Authorization':'Bearer '+access})
try:
    sql(f"""BEGIN;
    INSERT INTO tenant(id,name,slug) VALUES ('{home}','Fixture Home','{home}'),('{other}','Fixture Other','{other}');
    INSERT INTO tenant_product(tenant_id,product_code) VALUES ('{home}','aurora'),('{other}','aurora');
    INSERT INTO "AspNetUsers" ("Id","UserName","NormalizedUserName","PasswordHash","SecurityStamp","EmailConfirmed","PhoneNumberConfirmed","TwoFactorEnabled","LockoutEnabled","AccessFailedCount")
      VALUES ('{user}','{username}',upper('{username}'),'{encoded}','{user}',false,false,false,false,0);
    INSERT INTO user_tenant(tenant_id,user_id,is_default) VALUES ('{home}','{user}',true);
    INSERT INTO "AspNetUserRoles" SELECT '{user}',"Id",'{home}' FROM "AspNetRoles" WHERE "Name"='aurora:Admin';
    COMMIT;""")
    assert login(other).status==400
    assert login().status==200 and json.load(company(token()))['name']=='Fixture Home'
    sql(f'''UPDATE "AspNetUsers" SET "Aurora_User"=true WHERE "Id"='{user}';''')
    choices=login()
    assert choices.status==202 and other in [row['id'] for row in json.load(choices)] and token() is None
    assert login(str(uuid.uuid4())).status==400
    assert login(other).status==200
    access=token()
    assert json.load(company(access))['name']=='Fixture Other'
    sql(f"UPDATE tenant SET is_active=false WHERE id='{other}';")
    assert login(other).status==400 and company(access).status in (401,403)
    sql(f'''UPDATE "AspNetUsers" SET "Aurora_User"=false WHERE "Id"='{user}';''')
    assert sql(f"SELECT count(*) FROM user_tenant WHERE user_id='{user}' AND tenant_id='{other}';")=='0'
    assert login().status==200 and json.load(company(token()))['name']=='Fixture Home'
    print('PASS ordinary login, pending login, invalid selection, scoped PKCE token, inactive tenant and flag revocation.')
finally:
    sql(f"""DELETE FROM "AspNetUsers" WHERE "Id"='{user}'; DELETE FROM tenant WHERE id IN ('{home}','{other}');""")
