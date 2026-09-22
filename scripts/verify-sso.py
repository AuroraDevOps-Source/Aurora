"""Run on staging. Exercise the public PKCE flow without logging credentials or tokens."""
import base64
import hashlib
import http.cookiejar
from http.cookies import SimpleCookie
import json
from pathlib import Path
import secrets
import sys
import urllib.error
import urllib.parse
import urllib.request

authority = 'https://user.aurorasoftware.com'
state = json.loads(Path('/opt/aurora-auth/private/bootstrap.json').read_text())
cookies = http.cookiejar.CookieJar()

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None

http = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cookies), NoRedirect())
http.addheaders = [('User-Agent', 'Mozilla/5.0 AuroraStagingVerification/1.0')]

def request(url, payload=None, headers=None):
    try:
        return http.open(urllib.request.Request(url, data=payload, headers=headers or {}), timeout=40)
    except urllib.error.HTTPError as error:
        return error

if '--tms' in sys.argv:
    first_verifier = secrets.token_urlsafe(40)
    first_state = secrets.token_urlsafe(20)
    first_params = dict(client_id='auroratms-spa',redirect_uri='https://nova.aurorasoftware.net/auth/callback',
        response_type='code',scope='openid profile roles',state=first_state,
        code_challenge=base64.urlsafe_b64encode(hashlib.sha256(first_verifier.encode()).digest()).decode().rstrip('='),
        code_challenge_method='S256')
    first_query = urllib.parse.urlencode(first_params)
    for method in ('GET','POST'):
        anonymous_authorize = request(authority+'/connect/authorize'+('?' + first_query if method=='GET' else ''),
            None if method=='GET' else first_query.encode(),
            {} if method=='GET' else {'Content-Type':'application/x-www-form-urlencoded'})
        location = urllib.parse.urlsplit(anonymous_authorize.headers.get('Location',''))
        if anonymous_authorize.status!=302 or location.path!='/login':
            raise RuntimeError('Anonymous '+method+' authorization did not redirect to login')
        login_return = urllib.parse.parse_qs(location.query).get('returnUrl',[''])[0]
        if urllib.parse.urlsplit(login_return).path!='/connect/authorize' or urllib.parse.parse_qs(urllib.parse.urlsplit(login_return).query) != urllib.parse.parse_qs(first_query):
            raise RuntimeError('Login redirect lost the original PKCE request')
    silent = request(authority+'/connect/authorize?'+first_query+'&prompt=none')
    silent_location = urllib.parse.urlsplit(silent.headers.get('Location',''))
    if silent.status!=302 or urllib.parse.parse_qs(silent_location.query).get('error')!=['login_required']:
        raise RuntimeError('Silent anonymous authorization did not return login_required')
    print('Anonymous GET/POST login redirect, preserved PKCE request, silent login_required: OK')

login = request(authority + '/api/auth/login', json.dumps({
    'userName': state['admin_username'], 'password': state['admin_password']
}).encode(), {'Content-Type': 'application/json'})
print('Aurora login:', login.status)
if login.status != 200:
    print(login.read().decode()[:500])
    raise SystemExit(1)

login_cookies = SimpleCookie()
for header in login.headers.get_all('Set-Cookie', []):
    login_cookies.load(header)
auth_cookie = login_cookies.get('Aurora.Auth')
if auth_cookie is None or auth_cookie['expires'] or auth_cookie['max-age']:
    raise RuntimeError('Expected the existing nonpersistent Aurora login cookie')
if not auth_cookie['secure'] or not auth_cookie['httponly']:
    raise RuntimeError('Login cookie protections are missing')
print('Aurora login cookie: nonpersistent, Secure, HttpOnly')

if '--tms' in sys.argv:
    resumed=request(authority+login_return)
    returned=urllib.parse.parse_qs(urllib.parse.urlsplit(resumed.headers.get('Location','')).query)
    if resumed.status!=302 or returned.get('state')!=[first_state] or 'code' not in returned:
        raise RuntimeError('Original TMS authorization did not resume after login')
    redeemed=request(authority+'/connect/token',urllib.parse.urlencode(dict(grant_type='authorization_code',
        client_id='auroratms-spa',code=returned['code'][0],redirect_uri=first_params['redirect_uri'],code_verifier=first_verifier)).encode(),
        {'Content-Type':'application/x-www-form-urlencoded','Origin':'https://nova.aurorasoftware.net'})
    if redeemed.status!=200 or redeemed.headers.get('Access-Control-Allow-Origin')!='https://nova.aurorasoftware.net':
        raise RuntimeError('Resumed TMS authorization or browser token CORS failed')
    print('TMS authorization resumes after login; original verifier and token CORS: OK')

if '--embedded' in sys.argv:
    prepared = request(authority + '/api/auth/prepare-embedded-session', b'', {'Origin': authority})
    cookie = prepared.headers.get('Set-Cookie', '').lower()
    print('Embedded session:', prepared.status,
          'SameSite=None=', 'samesite=none' in cookie,
          'Secure=', '; secure' in cookie, 'HttpOnly=', '; httponly' in cookie)
    print('Issuer framing policy:', prepared.headers.get('Content-Security-Policy'))
    if prepared.status != 200:
        raise RuntimeError('Session preparation failed: ' + prepared.read().decode()[:500])
    if not all(attribute in cookie for attribute in ('samesite=none', '; secure', '; httponly')):
        raise RuntimeError('Embedded-session cookie attributes were not correct')
    for path in ('prepare-embedded-session', 'logout', 'login'):
        rejected = request(authority + '/api/auth/' + path, b'{}',
            {'Origin': 'https://untrusted.example', 'Content-Type': 'application/json'})
        print('Untrusted-origin', path, rejected.status)
        if rejected.status != 403:
            raise RuntimeError('Untrusted session-changing request was not blocked')
    anonymous = urllib.request.Request(authority + '/api/auth/prepare-embedded-session', data=b'',
        headers={'Origin': authority, 'User-Agent': 'Mozilla/5.0 AuroraStagingVerification/1.0'})
    try:
        urllib.request.urlopen(anonymous)
        raise RuntimeError('Anonymous session preparation was accepted')
    except urllib.error.HTTPError as error:
        print('Anonymous session preparation:', error.code)
        if error.code != 401:
            raise

clients = [
    ('aurora-spa', authority + '/authentication/login-callback', authority + '/api/v1/me/products'),
    ('freightops-spa', 'https://freightops.aurorasoftware.net/auth/callback', 'https://freightops.aurorasoftware.net/api/Authenticate/me'),
    ('hub-spa', 'https://integration.novafreightops.com/authentication/login-callback', 'https://integration.novafreightops.com/api/dashboard/summary'),
]
if '--tms' in sys.argv:
    clients.append(('auroratms-spa', 'https://nova.aurorasoftware.net/auth/callback', 'https://nova.aurorasoftware.net/api/auth/me'))

for client, callback, target in clients:
    verifier = secrets.token_urlsafe(40)
    challenge = base64.urlsafe_b64encode(hashlib.sha256(verifier.encode()).digest()).decode().rstrip('=')
    csrf = secrets.token_urlsafe(20)
    params = dict(client_id=client, redirect_uri=callback, response_type='code', scope='openid profile roles offline_access',
                  code_challenge=challenge, code_challenge_method='S256', state=csrf)
    authorized = request(authority + '/connect/authorize?' + urllib.parse.urlencode(params))
    if authorized.status != 302:
        print(client, 'authorize failed:', authorized.status, authorized.read().decode()[:500])
        raise RuntimeError(client + ' authorization failed')
    returned = urllib.parse.parse_qs(urllib.parse.urlparse(authorized.headers['Location']).query)
    if returned.get('state') != [csrf] or 'code' not in returned:
        print(client, 'authorization callback rejected:', returned.get('error_description'))
        raise RuntimeError(client + ' callback failed')
    exchanged = request(authority + '/connect/token', urllib.parse.urlencode(dict(
        grant_type='authorization_code', client_id=client, code=returned['code'][0],
        redirect_uri=callback, code_verifier=verifier)).encode(), {'Content-Type': 'application/x-www-form-urlencoded'})
    if exchanged.status != 200:
        print(client, 'exchange failed:', exchanged.status, exchanged.read().decode()[:500])
        raise RuntimeError(client + ' token exchange failed')
    token = json.loads(exchanged.read())['access_token']
    encoded_header = token.split('.')[0]
    print(client, 'token header:', json.loads(base64.urlsafe_b64decode(encoded_header + '=' * (-len(encoded_header) % 4))))
    encoded = token.split('.')[1]
    claims = json.loads(base64.urlsafe_b64decode(encoded + '=' * (-len(encoded) % 4)))
    print(client, 'identity:', {k: claims.get(k) for k in ('iss', 'aud', 'sub', 'tenant_id', 'role')})
    if client == 'auroratms-spa':
        endpoint = 'https://nova.aurorasoftware.net/api/auth/aurora'
        mismatch = request(endpoint, json.dumps({'accessToken': token}).encode(), {'Content-Type':'application/json','X-Tenant':'thor-xpress'})
        if mismatch.status != 403: raise RuntimeError('TMS tenant mismatch was not refused')
        local = request(endpoint, json.dumps({'accessToken': token}).encode(), {'Content-Type':'application/json'})
        if local.status != 200: raise RuntimeError('TMS session exchange failed: '+str(local.status)+' '+local.read().decode()[:300])
        session = json.loads(local.read())
        if session['user']['id'] != '9f21cec7-d066-4025-993c-321c5bbfa772': raise RuntimeError('Wrong linked TMS account')
        expected_roles = sorted(session['user']['roles'])
        native_claims = json.loads(base64.urlsafe_b64decode(session['accessToken'].split('.')[1]+'=='))
        if native_claims.get('auth_source') != 'aurora': raise RuntimeError('Missing SSO session marker')
        refreshed = request('https://nova.aurorasoftware.net/api/auth/refresh',json.dumps({'refreshToken':session['refreshToken']}).encode(),{'Content-Type':'application/json'})
        if refreshed.status != 200: raise RuntimeError('TMS refresh failed: '+str(refreshed.status))
        renewed = json.loads(refreshed.read())
        new_claims = json.loads(base64.urlsafe_b64decode(renewed['accessToken'].split('.')[1]+'=='))
        if sorted(renewed['user']['roles']) != expected_roles or new_claims.get('aurora_session_expires') != native_claims.get('aurora_session_expires'):
            raise RuntimeError('TMS refresh changed roles or extended the SSO session')
        token = renewed['accessToken']
        invalid = request(endpoint,json.dumps({'accessToken':'not-a-token'}).encode(),{'Content-Type':'application/json'})
        if invalid.status != 401: raise RuntimeError('TMS invalid-token rejection failed')
        print('TMS account link, tenant mismatch, refresh roles/expiry, invalid-token rejection: OK')
    response = request(target, headers={'Authorization': 'Bearer ' + token})
    body = response.read().decode()
    print(client, 'PKCE=OK', 'API status=', response.status)
    if client == 'aurora-spa' and response.status == 200:
        print('Products:', body)
        routing = request(authority + '/api/v1/routing/route-plans', headers={'Authorization': 'Bearer ' + token})
        print('Routing read:', routing.status)
        ptv = request(authority + '/api/v1/routing/optimizations', b'', {'Authorization': 'Bearer ' + token})
        print('PTV configured validation:', ptv.status, ptv.read().decode()[:200])
        if '--ptv' in sys.argv or '--ptv-quick' in sys.argv:
            boundary = 'AuroraSmoke' + secrets.token_hex(10)
            fixture = Path('/opt/aurora-auth/staging-smoke-manifest.json').read_bytes()
            payload = ('--' + boundary + '\r\nContent-Disposition: form-data; name="file"; filename="staging-smoke-manifest.json"\r\nContent-Type: application/json\r\n\r\n').encode() + fixture + ('\r\n--' + boundary + '--\r\n').encode()
            optimized = request(authority + '/api/v1/routing/optimizations', payload,
                {'Authorization': 'Bearer ' + token, 'Content-Type': 'multipart/form-data; boundary=' + boundary})
            data = json.loads(optimized.read())
            print('PTV synthetic end-to-end:', optimized.status, {k: data.get(k) for k in ('status', 'statusCode', 'summary', 'summaryError', 'detailWarning')})
            if data.get('status') != 'SUCCEEDED':
                print('PTV error:', data.get('responseBody', data.get('error', data)))
                raise RuntimeError('PTV synthetic optimization failed')
            if '--ptv-quick' in sys.argv:
                parts = []
                for name,filename,content in [('file','synthetic.json',fixture),('previousResult','previous.json',data['rawResponse'].encode())]:
                    parts.append(('--'+boundary+'\r\nContent-Disposition: form-data; name="'+name+'"; filename="'+filename+'"\r\nContent-Type: application/json\r\n\r\n').encode()+content+b'\r\n')
                parts.append(('--'+boundary+'\r\nContent-Disposition: form-data; name="mode"\r\n\r\nquick\r\n--'+boundary+'--\r\n').encode())
                quick = request(authority+'/api/v1/routing/optimizations',b''.join(parts),{'Authorization':'Bearer '+token,'Content-Type':'multipart/form-data; boundary='+boundary})
                updated = json.loads(quick.read())
                print('PTV quick update:', quick.status, {k:updated.get(k) for k in ('status','elapsedSeconds','summaryError')})
                if quick.status != 200 or updated.get('status') != 'SUCCEEDED':
                    print('PTV quick details:',updated)
                    raise RuntimeError('PTV Quick update failed')
    elif client == 'freightops-spa' and response.status == 200:
        data = json.loads(body)
        result = data.get('result', data)
        print('FreightOps linked username:', result.get('username'))
    elif client == 'hub-spa' and response.status == 200:
        data = json.loads(body)
        print('Hub authenticated dashboard response:', list(data) if isinstance(data, dict) else type(data).__name__)
    elif response.status != 200:
        print('Challenge:', response.headers.get('WWW-Authenticate'))
        print('Response:', body[:300])
        raise RuntimeError(client+' API verification failed')
