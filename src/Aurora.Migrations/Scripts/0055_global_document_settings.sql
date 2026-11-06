BEGIN;
CREATE TABLE public.global_document_settings (
 singleton boolean PRIMARY KEY DEFAULT true CHECK (singleton),
 instructions text NOT NULL CHECK (char_length(instructions)<=8000),
 revision integer NOT NULL DEFAULT 1
);
INSERT INTO public.global_document_settings(instructions) VALUES
($$Use the freight terms explicitly marked on the bill of lading (BOL). Prepaid: the shipper is the Bill To. Collect: the consignee is the Bill To. Use the identified party's printed name, billing or party address, and its own printed account code when available. Third Party (3rd Party): use the separately designated payer and its printed billing details. Leave missing details blank. If Third Party is marked but no payer is identified, flag the missing payer in Order.Notes. If the terms are missing, ambiguous, or conflicting, use only explicitly identified Bill To details and flag conflicts for review. Ignore unselected checkboxes and distinguish freight terms from COD instructions.$$);
CREATE FUNCTION public.can_edit_global_document_settings() RETURNS boolean
LANGUAGE sql STABLE SET search_path=pg_catalog,public AS $$
 SELECT EXISTS (SELECT 1 FROM public."AspNetUsers" u JOIN public."AspNetUserRoles" ur ON ur."UserId"=u."Id"
 JOIN public."AspNetRoles" r ON r."Id"=ur."RoleId"
 WHERE u."Id"=nullif(current_setting('app.user_id',true),'')::uuid AND u."Aurora_User"
 AND ur."TenantId"=nullif(current_setting('app.tenant_id',true),'')::uuid AND r."Name"='aurora:Admin')
$$;
ALTER TABLE public.global_document_settings ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.global_document_settings FORCE ROW LEVEL SECURITY;
CREATE POLICY read_global_rules ON public.global_document_settings FOR SELECT USING (EXISTS
 (SELECT 1 FROM public.user_tenant WHERE tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND user_id=nullif(current_setting('app.user_id',true),'')::uuid));
CREATE POLICY write_global_rules ON public.global_document_settings FOR UPDATE USING (public.can_edit_global_document_settings()) WITH CHECK (public.can_edit_global_document_settings());
GRANT SELECT, UPDATE(instructions,revision) ON public.global_document_settings TO aurora_app;
COMMIT;
