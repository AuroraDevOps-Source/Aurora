-- Global login changes affect every membership. Delegate only single-company, non-staff accounts.
CREATE FUNCTION public.aurora_managed_user_ids() RETURNS SETOF uuid
LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 WITH actor_roles AS (
   SELECT r."Name" AS name FROM public."AspNetUserRoles" ur
   JOIN public."AspNetRoles" r ON r."Id"=ur."RoleId"
   WHERE ur."TenantId"=nullif(current_setting('app.tenant_id',true),'')::uuid
     AND ur."UserId"=nullif(current_setting('app.user_id',true),'')::uuid
 )
 SELECT m.user_id FROM public.user_tenant m JOIN public."AspNetUsers" u ON u."Id"=m.user_id
 WHERE m.tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
 AND (EXISTS (SELECT FROM actor_roles WHERE name='aurora:Admin')
   OR (EXISTS (SELECT FROM actor_roles WHERE name='aurora:PowerUser')
     AND u."ManagementTenantId"=m.tenant_id AND NOT u."Aurora_User"
     AND NOT EXISTS (SELECT FROM public.user_tenant other WHERE other.user_id=m.user_id AND other.tenant_id<>m.tenant_id)
     AND NOT EXISTS (SELECT FROM public."AspNetUserRoles" ur JOIN public."AspNetRoles" r ON r."Id"=ur."RoleId"
                     WHERE ur."UserId"=m.user_id AND ur."TenantId"=m.tenant_id AND r."Name" IS DISTINCT FROM 'aurora:User')))
$$;
REVOKE ALL ON FUNCTION public.aurora_managed_user_ids() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.aurora_managed_user_ids() TO aurora_app;

CREATE POLICY power_user_create_membership ON public.user_tenant FOR INSERT TO aurora_app
 WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND is_default AND NOT aurora_staff_membership
   AND EXISTS (SELECT FROM public."AspNetUsers" u WHERE u."Id"=user_id AND u."ManagementTenantId"=tenant_id AND NOT u."Aurora_User")
   AND EXISTS (SELECT FROM public."AspNetUserRoles" ur JOIN public."AspNetRoles" r ON r."Id"=ur."RoleId"
               WHERE ur."TenantId"=tenant_id AND ur."UserId"=nullif(current_setting('app.user_id',true),'')::uuid AND r."Name"='aurora:PowerUser'));
