"""Offline completeness and linkage checks against the captured system metadata."""
import csv
import json
import re
from pathlib import Path
root=Path(__file__).resolve().parents[1]
load=lambda p: json.loads((root/p).read_text(encoding='utf-8-sig'))
d=load('map-data.json'); relations=load('metadata/relations.json'); columns=load('metadata/columns.json')
assert len(d['tables'])==222 and len(columns)==4224
assert {t['name'] for t in d['tables']}=={r['NAME'] for r in relations}
assert all(r['KIND']==0 for r in relations)
raw={(c['TABLE_NAME'],c['NAME']) for c in columns}
assert len(raw)==len(columns)
assert raw=={(t['name'],c['NAME']) for t in d['tables'] for c in t['columns']}
assert raw=={(c['T'],c['F']) for c in load('metadata/column-names.json')}
assert len(list((root/'tables').glob('*.md')))==222
assert sum(len(t['indexes']) for t in d['tables'])==len(load('metadata/indexes.json'))==331
assert sum(bool(t['pk']) for t in d['tables'])==215
assert all(c['KIND'] in ('PRIMARY KEY','NOT NULL') for c in load('metadata/constraints.json'))
assert len({e['id'] for e in d['relationships']})==298
for e in d['relationships']:
    assert len(e['columns'])==len(e['target_columns'])
    assert all((e['child'],c) in raw for c in e['columns'])
    assert all((e['parent'],c) in raw for c in e['target_columns'])
    assert e['kind']=='Inferred' and e['confidence'] in ('High','Medium','Low')
with (root/'relationships.csv').open(encoding='utf-8',newline='') as f:
    assert len(list(csv.DictReader(f)))==298
html=(root/'nova-database-map.html').read_text(encoding='utf-8')
assert 'TEMPLATE_NOTICE_START' not in html
assert '<body class="report-template">' not in html
assert '<script data-report-runtime type="text/plain">' not in html
template=(root/'scripts/report-template.html').read_text(encoding='utf-8')
assert 'href="../nova-database-map.html"' in template
assert '<script data-report-runtime type="text/plain">' in template
embedded=json.loads(re.search(r'<script id="atlas-data" type="application/json">(.*?)</script>',html,re.S).group(1))
assert embedded==d
assert not re.search(r'<(?:script|link|img)[^>]+(?:src|href)=["\']https?://',html)
for t in d['tables']:
    md=(root/'tables'/f'{t["name"]}.md').read_text(encoding='utf-8')
    assert all(c['NAME'] in md for c in t['columns'])
    diagram=(root/'diagrams'/(t['area'].lower().replace(' ','-')+'.mmd')).read_text(encoding='utf-8')
    assert f'{t["name"]}["{t["name"]}"]' in diagram
missing=[]
for p in root.rglob('*.md'):
    for href in re.findall(r'\]\(([^)]+)\)',p.read_text(encoding='utf-8')):
        if not href.startswith(('http:','https:','#')) and not (p.parent/href.split('#')[0]).exists(): missing.append((str(p),href))
assert not missing,missing
print('PASS: 222 tables; 4,224 unique columns; 215 PKs; 331 indexes; 298 valid logical mappings; 12 area diagrams; matching HTML payload; all Markdown links resolve.')
