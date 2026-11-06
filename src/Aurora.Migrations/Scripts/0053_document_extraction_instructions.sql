BEGIN;

ALTER TABLE public.tenant_settings ADD COLUMN document_extraction_instructions text NOT NULL DEFAULT ''
    CHECK (char_length(document_extraction_instructions) <= 16000);

-- Carry forward the form-specific instructions in use before this setting existed,
-- including for tenants that have never opened Settings.
INSERT INTO public.tenant_settings (tenant_id, document_extraction_instructions)
SELECT id, $$On a Lykes-issued authorization, transcribe the header labeled Tag # into BOTH TagNumber and PrintedLykesTag, even when the exact same value also appears under HAWB. Leave both blank if no Tag is printed. Never infer a Tag from HAWB alone. On Lykes forms the Tag # is the PRO source. Preserve its prefix in TagNumber; Aurora splits it.
Never treat file numbers, Ship IDs, tracking numbers, or another carrier's waybill/provider PRO as PrintedLykesTag. Preserve those identifiers in Notes.
On Lykes Cartage forms transcribe the Account header verbatim into PrintedBillToAccountLine. The account code before the first spaced dash is BillToAccountId; all text after it is Order.BillTo (including terminal suffixes). For example Account: EXP1034 - EXPEDITORS INTL - AUS TRANSCON, or PEG9001 - PEGASUS LOGISTICS - MCO.

On Pegasus and AIT forms with unlabeled dimensions leave DimensionUnit empty; Aurora applies the configured inches assumption.
For Pegasus forms capture the terminal printed above the waybill (e.g. DFW); do not substitute the Org/Dest airport codes.
For AIT forms capture the Delivery Terminal exactly, including a suffix such as TPA/X; Aurora removes the /X suffix.

On Forward Air forms keep the waybill in Order.Notes and the origin and destination separate. A signed consignee receipt date is the delivery completion date, not a requested appointment; never use an origin receipt date as the delivery schedule.
On Forward Air forms leave TerminalCode empty unless a separate servicing terminal is printed. Aurora uses DestinationTerminalCode for a Forward Air delivery when no explicit terminal or Lykes Tag prefix is available.

PICKUP ONLY and PICKUP + TRANSFER are Pickup; DELIVERY ONLY and RECOVER + DELIVERY are Delivery.$$
FROM public.tenant
ON CONFLICT (tenant_id) DO UPDATE SET document_extraction_instructions = EXCLUDED.document_extraction_instructions;

-- Ordinary importers can use their own company's instructions without reading connection settings.
CREATE FUNCTION public.aurora_document_extraction_instructions() RETURNS text
LANGUAGE sql STABLE SECURITY DEFINER SET search_path=pg_catalog,public AS $$
    SELECT COALESCE(s.document_extraction_instructions, '')
    FROM public.user_tenant m
    LEFT JOIN public.tenant_settings s ON s.tenant_id=m.tenant_id
    WHERE m.tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
      AND m.user_id=nullif(current_setting('app.user_id',true),'')::uuid
$$;
REVOKE ALL ON FUNCTION public.aurora_document_extraction_instructions() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.aurora_document_extraction_instructions() TO aurora_app;
COMMIT;
