ALTER TABLE aurora_order DROP CONSTRAINT aurora_order_status_check;
UPDATE aurora_order SET status=CASE status WHEN 'Available' THEN 'ReadyToRoute' WHEN 'Manifested' THEN 'Routed' ELSE status END;
ALTER TABLE aurora_order ALTER COLUMN status SET DEFAULT 'ReadyToRoute';
ALTER TABLE aurora_order ADD CONSTRAINT aurora_order_status_check CHECK(status IN
 ('Pending','ReadyToRoute','AwaitingAppointment','Routed','Dispatched','InTransit','OutForDelivery','Delivered','Voided'));
