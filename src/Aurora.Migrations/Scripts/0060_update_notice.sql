BEGIN;
CREATE TABLE public.update_notice (
    singleton boolean PRIMARY KEY DEFAULT true CHECK (singleton),
    update_at timestamptz
);
INSERT INTO public.update_notice DEFAULT VALUES;

ALTER TABLE public.update_notice ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.update_notice FORCE ROW LEVEL SECURITY;
CREATE POLICY read_update_notice ON public.update_notice FOR SELECT USING (EXISTS (
    SELECT 1 FROM public.user_tenant
    WHERE tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
      AND user_id = nullif(current_setting('app.user_id', true), '')::uuid
));
-- Use the same Aurora staff administrator privilege as global document settings.
CREATE POLICY publish_update_notice ON public.update_notice FOR UPDATE
    USING (public.can_edit_global_document_settings())
    WITH CHECK (public.can_edit_global_document_settings());
GRANT SELECT, UPDATE(update_at) ON public.update_notice TO aurora_app;
COMMIT;
