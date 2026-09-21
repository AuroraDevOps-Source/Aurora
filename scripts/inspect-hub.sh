set -e
docker exec integration-hub-postgres-1 psql -U postgres -d HubDB -Atc 'SELECT id, username FROM users ORDER BY id LIMIT 10'
docker exec integration-hub-postgres-1 psql -U postgres -d HubDB -Atc 'SELECT id, company_name FROM clients ORDER BY id LIMIT 10'
docker exec integration-hub-postgres-1 psql -U postgres -d HubDB -Atc "SELECT to_regclass('public.schemaversions')"
docker inspect integration-hub-hub-api-1 --format '{{range .Config.Env}}{{println .}}{{end}}' | cut -d= -f1
curl -sS -I --max-time 10 --resolve user.aurorasoftware.com:80:69.89.2.155 http://user.aurorasoftware.com/
