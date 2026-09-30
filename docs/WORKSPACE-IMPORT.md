# Company data import

Open the gear menu → Import as a company administrator. Download the example, choose a JSON file (maximum 25 MB), review the validated counts, then select **Replace company data**.

Replacement is one database transaction for the signed-in company. It removes its orders, manifests, trucks, terminals, customers, planning drafts, and saved route plans. Accounts, company settings, and other companies remain untouched. Equipment types remain available; supplied definitions update matching codes. Invalid records roll back the entire replacement.

The downloadable example is `src/Aurora.Client/wwwroot/samples/workspace-import.json`. It contains synthetic freight dated January 1, 2030.

Version 1 requires `version`, `terminals`, `customers`, `orders`, `manifests`, and `trucks`. All five collections must be arrays; an empty array clears that collection. Optional arrays are `equipmentTypes` and `planningSources`. Unknown properties are rejected to catch spelling errors.

- Terminals: unique `code`, `name`, address fields.
- Customers: unique `code`, `name`, address fields, optional `phone` and `email`.
- Orders: unique `id`, `customer`, `customerCode`, `terminalCode`, explicit `scheduledAt` with UTC offset, `status`, and shipment details. Codes reference records in the same file. Customer addresses are copied into order forms when selected; import order addresses are explicit snapshots.
- Trucks: unique `id`, `terminal` code, `typeCode`, availability, shift times, and UTC offset. New types must be defined in `equipmentTypes`.
- Manifests: unique `number`, `manifestDate`, `status`, `truckId`, and `orderIds`. An order can belong to one manifest, and all assigned orders must match the truck's terminal. Planning/Loading/Loaded/Ready manifests require Routed orders; Dispatched requires Dispatched; En Route/Arrived requires OutForDelivery; Complete requires Delivered.
- Planning sources: `name` and a routing `request` object, including locations, deliveries, and reporting terminal. Required for route optimization of those orders; without it orders can still be managed in the workspace but cannot be optimized. The example includes a source.

Internal database IDs and revisions are regenerated. This format replaces data; it does not merge records. A raw routing request alone is not a workspace import file.
