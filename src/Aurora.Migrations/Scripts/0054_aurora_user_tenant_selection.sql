BEGIN;
ALTER TABLE "AspNetUsers" ADD COLUMN "Aurora_User" boolean NOT NULL DEFAULT false;
ALTER TABLE user_tenant ADD COLUMN aurora_staff_membership boolean NOT NULL DEFAULT false;

CREATE FUNCTION public.aurora_login_tenants() RETURNS TABLE(id uuid,name text)
LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,public AS $$
 SELECT t.id,t.name FROM public.tenant t WHERE t.is_active AND EXISTS
 (SELECT 1 FROM public."AspNetUsers" u WHERE u."Id"=nullif(current_setting('app.user_id',true),'')::uuid AND u."Aurora_User")
 ORDER BY t.name,t.id
$$;

CREATE FUNCTION public.aurora_select_login_tenant(selected uuid) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
DECLARE actor uuid := nullif(current_setting('app.user_id',true),'')::uuid;
BEGIN
 PERFORM 1 FROM public."AspNetUsers" WHERE "Id"=actor AND "Aurora_User" FOR UPDATE;
 IF NOT FOUND OR NOT EXISTS (SELECT 1 FROM public.tenant WHERE id=selected AND is_active) THEN RETURN false; END IF;
 INSERT INTO public.user_tenant(tenant_id,user_id,aurora_staff_membership) VALUES(selected,actor,true)
 ON CONFLICT(tenant_id,user_id) DO NOTHING;
 IF EXISTS(SELECT 1 FROM public.user_tenant WHERE tenant_id=selected AND user_id=actor AND aurora_staff_membership) THEN
   DELETE FROM public."AspNetUserRoles" WHERE "TenantId"=selected AND "UserId"=actor;
   INSERT INTO public."AspNetUserRoles"("TenantId","UserId","RoleId")
   SELECT selected,actor,ur."RoleId" FROM public."AspNetUserRoles" ur
   JOIN public.user_tenant m ON m.user_id=actor AND m.is_default AND m.tenant_id=ur."TenantId"
   JOIN public."AspNetRoles" r ON r."Id"=ur."RoleId"
   WHERE ur."UserId"=actor AND r."Name" LIKE 'aurora:%';
 END IF;
 RETURN true;
END $$;

CREATE FUNCTION public.aurora_revoke_staff_memberships() RETURNS trigger
LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog,public AS $$
BEGIN
 DELETE FROM public.user_tenant WHERE user_id=NEW."Id" AND aurora_staff_membership;
 RETURN NEW;
END $$;
CREATE TRIGGER revoke_staff_memberships AFTER UPDATE OF "Aurora_User" ON "AspNetUsers"
 FOR EACH ROW WHEN (OLD."Aurora_User" AND NOT NEW."Aurora_User") EXECUTE FUNCTION public.aurora_revoke_staff_memberships();
REVOKE ALL ON FUNCTION public.aurora_revoke_staff_memberships(),public.aurora_select_login_tenant(uuid),public.aurora_login_tenants() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.aurora_select_login_tenant(uuid),public.aurora_login_tenants() TO aurora_app;
COMMIT;
