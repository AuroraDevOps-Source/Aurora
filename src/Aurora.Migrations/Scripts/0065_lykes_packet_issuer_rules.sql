BEGIN;
-- Add defaults only for this tenant and only where an issuer-wide rule does not already exist.
WITH defaults(issuer,instructions) AS (VALUES
 ('Maverick Acquisition Ventures INC',$r$On a Maverick form use the printed Bill No. as BillOfLading. Copy the entire value including MAV and leading zeroes. This is a resolved BOL: omit the BOL question when a single Bill No. is legible. Leave Order.Id, TagNumber and PrintedLykesTag empty unless a separate explicitly labeled PRO is present; for a separate PRO, copy it into Order.Id and include its printed label and value in PrintedIdentifiers. Never put the bill number in PRO.$r$),
 ('Xpeditious Unlimited',$r$On a Destination Delivery Order use Way Bill Number as the shipment BOL, with the complete prefix. Distinguish the shipment waybill from the file number, job reference and flight. Take the consignee from the consignee block, deadline from Delivery Deadline, and preserve both skid and piece counts from the totals.$r$),
 ('Allstates WorldCargo',$r$On WorldCargo sheets keep individual shipments separate even when shipper, consignee and freight class repeat. Capture the station-prefixed identifier repeated at the top and by the footer barcode, the other numeric header identifier, and each party reference under its own label. If no BOL or HAWB is labeled and competing numbers remain, offer those identifiers for review. A checked Bill Third Party box identifies the payer in the Third Party block. Use its address. Read the actual weight, pieces and delivery due date from their labeled cells.$r$),
 ('Capital Logistics International LLC',$r$On the Delivery Order use B/L OR AWB NO as BillOfLading, preserving leading zeros. Read HAWB NO only from its own cell; ENTRY NO and CUST REF NO are adjacent and independent. Take the consignee from FOR DELIVERY TO and payer from FREIGHT CHARGES FOR ACCOUNT OF. Use the package count and labeled pound weight; the kilogram figure is the same cargo.$r$),
 ('R+L Carriers',$r$On the Straight Bill of Lading use the explicitly named BILL TO/THIRD PARTY and its address. PREPAID alone does not replace that payer. Keep Customer Code, Shippers No., Quote No. and Purchase Order No. separate. An empty PRO label area has no PRO. When no explicit BOL identifier is given, offer the actual printed identifiers for review.$r$),
 ('Beyond Transportation Inc.',$r$On a Carrier Confirmation use Pickup and Appointment in Routing Instructions for work type, location and time. The carrier information block identifies the assigned carrier. Read Probill #, BOL No./Reference No., P/U Ref., and Carrier Confirmation number as distinct fields; do not shift a value into an adjacent empty column. Use a populated BOL No./Reference No. cell for BOL. Leave an empty cell for review. Read skids and pounds from Freight Specifics.$r$),
 ('PGL',$r$On Road Freight LTL Arrival Cartage Advice, use HOUSE BILL as HAWB and BOL, and MASTER BILL as MAWB. Keep SHIPMENT, CONSOL and ORDER NUMBERS / REFERENCE separate. Use PICKUP and DELIVER TO for the stops. Read weight in LB as a decimal, and use the total row once without adding the detail row again.$r$),
 ('Alliance Store Fixtures Inc.',$r$On the Alliance Bill of Lading group copies of the same filled form; include identifiers on a stamped copy and count skids and weight only once. A PRO in a carrier stamp is a PRO, not a BOL. Split the Reference # and P.O. values even if they occupy one box. Use Exporter/Seller/Consignor and Ship To/Consignee for the parties, and Shipping Via/Carrier for carrier. Broker-paid customs fees alone do not identify the freight payer. Retain a review question for an unidentified BOL.$r$),
 ('Laufer Group International',$r$On a Trucker’s Delivery Order read HAWB Number as HAWB and BOL; keep MAWB Number and Laufer Ref# separate. Cargo Loc is the pickup and For Delivery To is the consignee. A Q# in comments is a quote reference, never a PRO. Use the labeled pounds once when KG and LBS are both shown.$r$)
), additions AS (
 SELECT s.tenant_id,jsonb_agg(jsonb_build_object('Issuer',d.issuer,'Form','','Account','','Instructions',d.instructions) ORDER BY d.issuer) AS rules
 FROM public.tenant_settings s CROSS JOIN defaults d
 WHERE s.tenant_id='11111111-1111-1111-1111-111111111111'
 AND EXISTS (SELECT FROM public.tenant t WHERE t.id=s.tenant_id AND lower(trim(t.name))='lykes cartage')
 AND NOT EXISTS (SELECT FROM jsonb_array_elements(s.document_form_rules) r WHERE upper(trim(r->>'Issuer'))=upper(d.issuer)
 AND coalesce(trim(r->>'Form'),'')='' AND coalesce(trim(r->>'Account'),'')='')
 GROUP BY s.tenant_id
)
UPDATE public.tenant_settings s SET document_form_rules=s.document_form_rules || a.rules,revision=s.revision+1
FROM additions a WHERE s.tenant_id=a.tenant_id;
COMMIT;
