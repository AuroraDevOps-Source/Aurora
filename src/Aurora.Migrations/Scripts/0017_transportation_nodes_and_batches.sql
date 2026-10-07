-- Nova 1.5 planning workflow: transportation node attributes, planning batches, and the
-- "Optimization in Process" lock (an order is locked while an open plan contains it).
ALTER TABLE aurora_terminal ADD COLUMN details jsonb NOT NULL DEFAULT '{}';

-- Imported terminals already have a depot position in their planning source; keep it as the node's coordinates.
UPDATE aurora_terminal t
SET details = t.details || jsonb_build_object('Latitude', (l->>'latitude')::double precision, 'Longitude', (l->>'longitude')::double precision)
FROM aurora_order_source s, jsonb_array_elements(CASE WHEN jsonb_typeof(s.request->'locations') = 'array' THEN s.request->'locations' ELSE '[]'::jsonb END) l
WHERE s.tenant_id = t.tenant_id
  AND lower(trim(s.request#>>'{reporting,terminal}')) = lower(t.code)
  AND l->>'id' = s.request#>>'{depots,0,locationId}'
  AND NOT (t.details ? 'Latitude');

ALTER TABLE aurora_planning_draft ADD COLUMN name text NOT NULL DEFAULT 'Selected orders';
ALTER TABLE aurora_planning_draft ADD COLUMN batch text NOT NULL DEFAULT 'General';
ALTER TABLE aurora_planning_draft ADD COLUMN cancelled_at timestamptz;
CREATE INDEX ix_aurora_planning_draft_open ON aurora_planning_draft(tenant_id, source_id)
 WHERE finished_session IS NULL AND cancelled_at IS NULL;
