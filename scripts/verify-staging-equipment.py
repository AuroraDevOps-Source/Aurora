"""Authenticated equipment persistence and synthetic planner tests on staging. Never logs tokens."""
import argparse
import base64
import copy
import hashlib
import http.cookiejar
import json
from pathlib import Path
import secrets
import urllib.error
import urllib.parse
import urllib.request

parser = argparse.ArgumentParser()
parser.add_argument('release')
args = parser.parse_args()
root = Path('/opt/aurora-auth')
source = root / 'releases' / args.release
authority = 'https://user.aurorasoftware.com'
state = json.loads((root / 'private/bootstrap.json').read_text())
class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None
http = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()), NoRedirect())
http.addheaders = [('User-Agent', 'Mozilla/5.0 AuroraEquipmentTest/1.0')]
def call(path, payload=None, method=None, headers=None):
    try:
        return http.open(urllib.request.Request(authority + path, data=payload, method=method, headers=headers or {}), timeout=120)
    except urllib.error.HTTPError as error:
        return error
def check(condition, message):
    if not condition: raise RuntimeError(message)
    print('PASS:', message, flush=True)
login = call('/api/auth/login', json.dumps({'userName':state['admin_username'], 'password':state['admin_password']}).encode(), headers={'Content-Type':'application/json'})
check(login.status == 200, 'test account login')
verifier = secrets.token_urlsafe(40)
csrf = secrets.token_urlsafe(20)
callback = authority + '/authentication/login-callback'
params = dict(client_id='aurora-spa', redirect_uri=callback, response_type='code', scope='openid profile roles', code_challenge=base64.urlsafe_b64encode(hashlib.sha256(verifier.encode()).digest()).decode().rstrip('='), code_challenge_method='S256', state=csrf)
auth = call('/connect/authorize?' + urllib.parse.urlencode(params))
returned = urllib.parse.parse_qs(urllib.parse.urlparse(auth.headers.get('Location','')).query)
check(auth.status == 302 and returned.get('state') == [csrf] and 'code' in returned, 'PKCE authorization')
exchange = call('/connect/token', urllib.parse.urlencode(dict(grant_type='authorization_code',client_id='aurora-spa',code=returned['code'][0],redirect_uri=callback,code_verifier=verifier)).encode(), headers={'Content-Type':'application/x-www-form-urlencoded'})
check(exchange.status == 200, 'PKCE token exchange')
token = json.load(exchange)['access_token']
headers = {'Authorization':'Bearer ' + token, 'Content-Type':'application/json'}
def api(path, value=None, method=None):
    response = call('/api/v1/routing/' + path, None if value is None else json.dumps(value).encode(), method, headers)
    body = response.read()
    return response.status, json.loads(body) if body else None
check(call('/api/v1/routing/equipment').status == 401, 'anonymous catalog access rejected')
samples = json.loads((source / 'client/wwwroot/samples/equipment-catalog.json').read_text())
check(api('equipment/import', samples, 'POST')[0] == 200, 'spreadsheet samples imported into the test database')
status, catalog = api('equipment')
check(status == 200 and {t['Code'] for t in samples['Types']} <= {t['code'] for t in catalog['types']} and {u['Id'] for u in samples['Units']} <= {u['id'] for u in catalog['units']}, 'all 14 types and 279 sample units reload')
unit = next(u for u in catalog['units'] if u['id'] == samples['Units'][0]['Id'])
edited = copy.deepcopy(unit)
edited['available'] = not edited['available']
try:
    check(api('equipment/units', edited, 'PUT')[0] == 200, 'unit availability save')
    check(next(u for u in api('equipment')[1]['units'] if u['id'] == unit['id'])['available'] == edited['available'], 'availability persisted across requests')
finally:
    check(api('equipment/units', unit, 'PUT')[0] == 200, 'original sample availability restored')
check(api('equipment/types', {'code':'INVALID-TEST','description':'Invalid negative capacity','weight':-1}, 'PUT')[0] == 400, 'invalid equipment capacity rejected')
check(api('equipment/import', samples, 'POST')[0] == 200, 'sample reimport is safe')
check(len(api('equipment')[1]['units']) == len(catalog['units']), 'reimport creates no duplicate units')

fixture = json.loads((source / 'client/wwwroot/samples/appointment-test.json').read_text())
fixture['settings']['duration'] = 5
for i, vehicle in enumerate(fixture['vehicles']):
    equipment = next(t for t in catalog['types'] if t['code'] == ('TEST-26 BOXL' if i == 0 else 'TEST-16 BOX'))
    vehicle['constraints'] = {'maximumLoads':[{'dimension':'weight','value':equipment['weight']},{'dimension':'volume','value':equipment['cubes']}], 'route':{'maximumNumberOfStops':10}}
    vehicle['start']['earliestStartTime'] = '2030-01-01T08:30:00-06:00'
    vehicle['end']['latestEndTime'] = '2030-01-01T16:30:00-06:00'
for order in fixture['orders']['deliveries']:
    order['properties'] = {'loads':[{'dimension':'weight','value':100},{'dimension':'volume','value':20}]}
    order['delivery']['duration'] = 1200
    report = fixture['reporting']['orders'][order['id']]
    report.update(units=2,address1='Synthetic test address',city='Test city',state='MO',postalCode='64116')
    if order['id'] != 'TEST_EARLY':
        report['appointment'].update(blockBegin='2030-01-01T10:00:00-06:00',blockEnd='2030-01-01T10:00:00-06:00')
def optimize(data, name):
    boundary = 'AuroraEquipment' + secrets.token_hex(10)
    payload = (f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{name}.json"\r\nContent-Type: application/json\r\n\r\n').encode() + json.dumps(data).encode() + f'\r\n--{boundary}--\r\n'.encode()
    result = call('/api/v1/routing/optimizations', payload, headers={'Authorization':'Bearer ' + token,'Content-Type':'multipart/form-data; boundary=' + boundary})
    report = json.load(result)
    check(result.status == 200 and report.get('summary') is not None, name + ' optimizer response')
    (root / 'private' / (name + '-result.json')).write_text(json.dumps(report, indent=2))
    print(name, json.dumps(report['summary']), flush=True)
    check(report['summary']['violations'] == 0, name + ' has no constraint violations')
    return report
two = optimize(fixture, 'equipment-two-truck-test')
check(two['summary']['scheduled'] == 2 and two['summary']['unscheduled'] == 1, 'two trucks meet simultaneous edited appointments')
pros = [p for route in two['routes'] for p in route['pros']]
check(all(p['units'] == 2 and p['weight'] == 100 and p['shipToName'].startswith('Synthetic') and p['allottedStopSeconds'] == 1200 for p in pros), 'Ship to, Units, Weight and edited service time reach the manifest')
check(all('T10:00:00' in p['arrival'] for p in pros), 'edited appointment arrival time is enforced')
check(all(r['budgetedSeconds'] == 8*3600 for r in two['routes']), 'edited departure and return produce an eight-hour budget')
fixture['vehicles'] = fixture['vehicles'][:1]
one = optimize(fixture, 'equipment-one-truck-test')
check(one['summary']['scheduled'] == 1 and one['summary']['unscheduled'] == 2, 'reducing the fleet changes the feasible schedule')
print('Equipment and planner verification complete.', flush=True)
