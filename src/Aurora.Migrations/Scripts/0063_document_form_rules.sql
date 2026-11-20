BEGIN;
ALTER TABLE public.tenant_settings ADD COLUMN document_form_rules jsonb NOT NULL DEFAULT '[]'
    CHECK (jsonb_typeof(document_form_rules)='array');
-- The initial customer override applies only to the Lykes account reviewed with operations.
INSERT INTO public.tenant_settings(tenant_id,document_form_rules)
SELECT id,jsonb_build_array(jsonb_build_object(
    'Issuer','Lykes Cartage Company','Form','Lykes Freight Bill','Account','UPS9001',
    'Instructions','For this UPS Supply Chain account keep the complete printed account name, including branch text. Read the bill-to code from Account, the PRO from Tag #, and the origin terminal from Org. Leave unknown payer address fields blank for the Nova account lookup.'))
FROM public.tenant WHERE lower(trim(name))='lykes cartage' AND id='11111111-1111-1111-1111-111111111111'
ON CONFLICT(tenant_id) DO UPDATE SET document_form_rules=excluded.document_form_rules,revision=tenant_settings.revision+1;
CREATE FUNCTION public.aurora_document_form_rules() RETURNS jsonb
LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,public AS $$
    SELECT coalesce(s.document_form_rules,'[]'::jsonb)
    FROM public.user_tenant m LEFT JOIN public.tenant_settings s ON s.tenant_id=m.tenant_id
    WHERE m.tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
      AND m.user_id=nullif(current_setting('app.user_id',true),'')::uuid
$$;
REVOKE ALL ON FUNCTION public.aurora_document_form_rules() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.aurora_document_form_rules() TO aurora_app;
COMMIT;
