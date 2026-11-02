-- Freeze the first target precondition across lost responses, imports and worker restarts.
CREATE TABLE aurora_freightops_request (
 tenant_id uuid NOT NULL,
 connection_id uuid NOT NULL,
 change_id uuid NOT NULL,
 -- Preserve exact JSON property order/number spelling for the request hash on retries.
 request text NOT NULL CHECK(jsonb_typeof(request::jsonb)='object'),
 created_at timestamptz NOT NULL DEFAULT now(),
 PRIMARY KEY(tenant_id,connection_id,change_id),
 FOREIGN KEY(tenant_id,connection_id) REFERENCES aurora_sync_connection(tenant_id,id),
 FOREIGN KEY(tenant_id,change_id) REFERENCES aurora_sync_outbox(tenant_id,id)
);
ALTER TABLE aurora_freightops_request ENABLE ROW LEVEL SECURITY;
ALTER TABLE aurora_freightops_request FORCE ROW LEVEL SECURITY;
CREATE POLICY sync_tenant ON aurora_freightops_request
 USING(tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid)
 WITH CHECK(tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid);
-- Application requests are immutable. Administrative cleanup follows outbox retention.
GRANT SELECT,INSERT ON aurora_freightops_request TO aurora_app;
