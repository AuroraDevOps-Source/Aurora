BEGIN;
-- Add the first Waylanders form without replacing other forms or an administrator's saved rule.
WITH defaults AS (
    SELECT jsonb_build_array(jsonb_build_object(
        'Issuer','Article','Form','Delivery Ticket','Account','','UsePrintedPro',true,
        'Instructions',$article$Apply only to Article Delivery Ticket forms (Article is the issuer; Waylanders Delivery is the carrier). Set MappingSender to Article, MappingForm to Delivery Ticket and Order.WorkType to Delivery.
Use the complete printed Order # as BillOfLading, preserving leading zeros. Include it in PrintedIdentifiers under Order #. This is a resolved BOL mapping when one legible order number is printed. The repeated ORDER ID/# in the freight body is the same order, not a second shipment. Leave Order.Id, TagNumber and PrintedLykesTag blank unless a separate PRO is explicitly printed; never use Order # as PRO.
The recipient block below the Article heading is the uncoded consignee. Copy its name, street, city and postal code into the consignee fields. Leave ConsigneeAccountId blank. Preserve the recipient phone and email in Order.Notes with labels. Do not infer a missing state, country or account code from the city or ZIP.
Extract every freight row, including continuation pages, with Box ID and product description together in Description, Qty as Pieces, the three printed dimensions, and the weight marked lbs as WeightLb. Preserve decimal weights. Do not assume dimension units, multiply a printed row weight by quantity, or count repeated headings as freight. Do not use sample values as defaults.
Preserve the exact Delivery Service wording in Order.Notes labeled Delivery Service (for example Ground-level Door). No service-level or job-type code has been agreed; do not invent one. Preserve Delivery Instruction separately and verbatim. A no-earlier-than instruction is a lower bound, not a confirmed appointment; retain it in notes. A browser print timestamp is not a shipment date. Do not invent the year for an incomplete delivery date.
Keep absent shipper and payer details blank for review under the existing billing rules. Carrier: Waylanders Delivery is not billing evidence. Annotation arrows, colored labels, browser controls, signature/date blanks and comments blanks are not shipment values.$article$)) AS rules
)
INSERT INTO public.tenant_settings(tenant_id,document_form_rules)
SELECT t.id,d.rules FROM public.tenant t CROSS JOIN defaults d
WHERE lower(trim(t.name)) IN ('waylanders','waylanders delivery')
ON CONFLICT(tenant_id) DO UPDATE
SET document_form_rules=tenant_settings.document_form_rules || excluded.document_form_rules,
    revision=tenant_settings.revision+1
WHERE NOT EXISTS (
    SELECT FROM jsonb_array_elements(tenant_settings.document_form_rules) r
    WHERE upper(rtrim(trim(r->>'Issuer'),'.'))='ARTICLE'
      AND upper(trim(coalesce(r->>'Form',''))) IN ('','DELIVERY TICKET')
      AND trim(coalesce(r->>'Account',''))=''
);
COMMIT;
