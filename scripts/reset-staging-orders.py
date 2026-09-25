"""Reset ONLY Aurora Demo Co orders/plans on the named test deployment.
Run before restarting the API during publication. Retains fleet and identity data.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import time

TENANT = "11111111-1111-1111-1111-111111111111"
ROOT = Path("/opt/aurora-auth")

def sql(query):
    return subprocess.run(["docker", "exec", "-i", "aurora-auth-db", "psql", "-X", "-qAt", "-v", "ON_ERROR_STOP=1", "-U", "postgres", "-d", "aurora"], input=query, text=True, capture_output=True, check=True).stdout.strip()

def literal(value):
    return "'" + json.dumps(value).replace("'", "''") + "'::jsonb"

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args=parser.parse_args()
    if not (ROOT/"private/bootstrap.json").is_file():
        raise RuntimeError("This helper only runs on the Aurora test deployment")
    if sql(f"SELECT name FROM tenant WHERE id='{TENANT}'") != "Aurora Demo Co":
        raise RuntimeError("Expected demo tenant not found")
    baseline_path=ROOT/"private/demo-order-baseline.json"
    if baseline_path.exists():
        baseline=json.loads(baseline_path.read_text())
    else:
        sources=json.loads(sql(f"SELECT json_agg(s) FROM aurora_order_source s WHERE tenant_id='{TENANT}' AND name='Appointment test orders'"))
        if not sources or len(sources)!=1: raise RuntimeError("Expected one appointment test source")
        source=sources[0]["id"]
        orders=json.loads(sql(f"SELECT json_agg(o ORDER BY id) FROM aurora_order o WHERE tenant_id='{TENANT}' AND source_id='{source}'"))
        baseline={"sources":sources,"orders":orders}
    orders=baseline["orders"]
    if len(orders)!=100 or {o["id"] for o in orders}!={f"LTL{i:04d}" for i in range(1,101)}:
        raise RuntimeError("Baseline must contain exactly the original 100 test PROs")
    if any(row["tenant_id"]!=TENANT for rows in baseline.values() for row in rows):
        raise RuntimeError("Baseline contains another tenant")
    for order in orders: order["status"]="ReadyToRoute"
    users=sql(f"SELECT user_id FROM user_tenant WHERE tenant_id='{TENANT}'").splitlines()
    owners=[hashlib.sha256((TENANT+":"+user).encode()).hexdigest().upper() for user in users]
    mounts=json.loads(subprocess.run(["docker","inspect","aurora-auth-api","--format","{{json .Mounts}}"],capture_output=True,text=True,check=True).stdout)
    key_mount=next(m for m in mounts if m["Destination"]=="/keys" and m["Type"]=="volume")
    key_root=Path(key_mount["Source"]).resolve()
    sessions=(key_root/"routing-sessions").resolve()
    if sessions.parent!=key_root or not str(key_root).startswith("/var/lib/docker/volumes/"):
        raise RuntimeError("Unexpected session volume")
    protected=sql(f"SELECT json_build_object('fleet',(SELECT json_agg(u ORDER BY id) FROM routing_equipment_unit u WHERE tenant_id='{TENANT}'),'users',(SELECT json_agg(u ORDER BY \"Id\") FROM \"AspNetUsers\" u))")
    if args.check:
        print("Reset validated: original 100 orders; demo plans only; fleet and logins retained.")
        return
    if not baseline_path.exists():
        with baseline_path.open("x") as f:
            baseline_path.chmod(0o600)
            json.dump(baseline,f)
    stamp=time.strftime("%Y%m%d-%H%M%S")
    archive=ROOT/"private"/("orders-pre-reset-"+stamp)
    archive.mkdir(mode=0o700)
    with (archive/"database.dump").open("xb") as f:
        subprocess.run(["docker","exec","aurora-auth-db","pg_dump","-U","postgres","-d","aurora","-Fc"],stdout=f,check=True)
    subprocess.run(["docker","stop","aurora-auth-api"],check=True,capture_output=True)
    try:
        statements=["BEGIN;"]
        for table in ["aurora_manifest_order","aurora_manifest","aurora_planning_draft","aurora_order","aurora_order_source"]:
            statements.append(f"DELETE FROM {table} WHERE tenant_id='{TENANT}';")
        for table,key in [("aurora_order_source","sources"),("aurora_order","orders")]:
            statements.append(f"INSERT INTO {table} SELECT * FROM jsonb_populate_recordset(NULL::{table}, {literal(baseline[key])});")
        statements.append("COMMIT;")
        sql("\n".join(statements))
        # Archive only this tenant's session files, never the shared encryption keys.
        for owner in owners:
            for file in sessions.glob(owner+"-*.json"):
                if file.resolve().parent!=sessions or file.is_symlink(): raise RuntimeError("Unsafe session file")
                file.rename(archive/file.name)
        after=sql(f"SELECT json_build_object('fleet',(SELECT json_agg(u ORDER BY id) FROM routing_equipment_unit u WHERE tenant_id='{TENANT}'),'users',(SELECT json_agg(u ORDER BY \"Id\") FROM \"AspNetUsers\" u))")
        if protected!=after: raise RuntimeError("Fleet or users unexpectedly changed")
        count=sql(f"SELECT count(*) FROM aurora_order WHERE tenant_id='{TENANT}' AND status='ReadyToRoute'")
        if count!="100": raise RuntimeError("Reset verification failed")
        print("Reset complete: 100 Ready to Ship orders, no demo manifests/drafts. Trucks, tenants and logins preserved. Backup: "+str(archive))
    finally:
        subprocess.run(["docker","start","aurora-auth-api"],check=True,capture_output=True)

if __name__=="__main__": main()
