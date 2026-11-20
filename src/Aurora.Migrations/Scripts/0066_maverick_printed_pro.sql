BEGIN;
UPDATE public.tenant_settings s SET document_form_rules=(
    SELECT jsonb_agg(CASE WHEN r->>'Issuer'='Maverick Acquisition Ventures INC' AND r->>'Form'='' AND r->>'Account'=''
        THEN r || '{"UsePrintedPro":true}'::jsonb ELSE r END ORDER BY n)
    FROM jsonb_array_elements(s.document_form_rules) WITH ORDINALITY AS rule(r,n)
),revision=revision+1
WHERE s.tenant_id='11111111-1111-1111-1111-111111111111'
AND s.document_form_rules @> '[{"Issuer":"Maverick Acquisition Ventures INC","Form":"","Account":""}]'::jsonb;
COMMIT;
