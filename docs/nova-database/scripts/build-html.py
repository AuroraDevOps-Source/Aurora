"""Generate the self-contained HTML; contains metadata only and needs no server."""
import json
import re
from pathlib import Path
root = Path(__file__).resolve().parents[1]
data = json.loads((root/'map-data.json').read_text(encoding='utf-8'))
template = (root/'scripts/report-template.html').read_text(encoding='utf-8')
template = re.sub(r'<!-- TEMPLATE_NOTICE_START -->.*?<!-- TEMPLATE_NOTICE_END -->', '', template, flags=re.S)
template = template.replace('<body class="report-template">', '<body>')
template = template.replace('<script data-report-runtime type="text/plain">', '<script>')
payload = json.dumps(data,ensure_ascii=False,separators=(',',':')).replace('<','\\u003c').replace('>','\\u003e').replace('&','\\u0026')
(root/'nova-database-map.html').write_text(template.replace('__DATA__',payload),encoding='utf-8')
print('Wrote nova-database-map.html:',len(data['tables']),'tables;',len(data['relationships']),'logical relationships')
