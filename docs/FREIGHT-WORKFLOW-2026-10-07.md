# Freight workflow foundation

Saved orders now expose **Freight workflow** in the Orders grid. The workflow keeps the
original order's shipper, consignee, and bill-to independent from the locations and
providers performing individual pieces of work. Existing orders require no backfill:
the first workflow GET returns an empty revision-zero plan.

## Operator workflow

1. Open an order's Freight workflow and use **Use order locations**, or add activities.
   Transportation-node selections copy address snapshots. Review missing addresses
   before saving; an imported order may not contain a complete origin.
2. Set freight bill type, service promised, currency, tariff/agreement reference, and
   company/partner/GBL/other shipment references. These are descriptive references,
   not an automatic tariff lookup.
3. Add movement legs, warehouse work, and storage as separate activity kinds. Movements
   have origin and destination; warehouse/storage activities have one location. Each
   activity identifies the company's role, provider type/name, and instructions.
4. Add ordered tasks such as unload, inspect, blanket wrap, assemble, and load. Required
   tasks must finish in sequence. Optional tasks may be omitted. Enter activity and
   task costs/charges separately, in the workflow currency.
5. Save, then start activities and record task completion, completion, failure, or
   cancellation. Execution is available even after manifest dispatch. Event times and
   user IDs come from the server. Failure/cancellation requires a reason; failures
   include responsibility for billing review.
6. For a failed movement, **Plan another attempt** appends a new movement linked to the
   failed one. Review its addresses and charges; insert a return-to-dock movement when
   appropriate. Completed/failed history is never reopened.

An activity can start only after preceding activities finish, fail, or are cancelled.
Once execution reaches an activity, its definition and the preceding sequence are
frozen. Future work can still be edited or appended. This first version models a
sequential workflow; it does not support parallel task dependencies.

Storage starts at its explicit Start event and ends at completion, failure, or
cancellation. The estimate is `ceil(max(0, elapsed hours - free hours) / 24) * daily rate`,
per activity, with exact grace and 24-hour boundaries covered by checks. Refresh to
update a live estimate. It is not a calendar-day tariff or a per-piece charge.

Planned charges, estimated costs, recorded actual costs, and accrued storage estimates
are shown separately. Actual activity costs should exclude costs already recorded on
tasks. Missing actual costs mean unknown, not measured zero. No amount is posted to an
invoice, AR, AP, or ledger by this feature.

## Persistence and API

Apply migration `0018_freight_workflow.sql` before running the new API. It adds
`aurora_freight_workflow`, keyed by tenant/source/order, with forced tenant RLS, a
revision, and a JSON aggregate. No existing order or manifest data is rewritten.

- `GET /api/v1/tms/orders/{source}/{id}/workflow`
- `PUT /api/v1/tms/orders/{source}/{id}/workflow` — saves a plan using its current revision.
  Client-supplied history is discarded in favor of saved server history.
- `POST /api/v1/tms/orders/{source}/{id}/workflow/events` — accepts a `FreightExecution`
  command with revision, unique command ID, activity ID, optional task ID, action,
  note, responsibility, and optional actual cost.

All endpoints require authentication and use the existing tenant connection factory.
The owning order is locked transactionally to serialize plan saves, execution, and
order deletion. Stale writes return HTTP 409. An identical execution retry is
idempotent, even with an old revision; reuse with different data is rejected.
Node references are checked within the tenant. Existing snapshots on executed work
remain usable if a node is subsequently removed.

Ordinary order edits and routing imports do not overwrite the workflow. Executed
orders cannot be soft-deleted. Whole-workspace replacement is rejected when the
tenant has any saved freight workflows, because that destructive importer cannot
preserve their parent identities. There is deliberately no workflow-delete endpoint.

## Boundaries and next integrations

- Existing dispatch/optimization still assign whole orders to manifests. Workflow
  legs do **not** yet drive optimizer destinations, independent leg manifests, or
  order status. The UI calls out this distinction. Multiple manifest assignments
  per freight bill require a migration of the existing membership model.
- Execution is entered manually. FreightOps scans, driver updates, and partner
  integrations should submit validated execution commands through an adapter.
  Historical occurrence timestamps, corrections, and external event provenance
  need a defined policy before that adapter is enabled.
- Provider names and pricing-agreement references are descriptive. Carrier account
  links, tariff evaluation, billable exception approval, split invoices, detention
  notifications, account-level storage defaults, and automatic costing remain open.
- A recovery can be a separate order with bill type Recovery and an Original shipment
  reference. There is no automatic recovery-order creation or enforced cross-order link.
- Service templates, structured equipment requirements, shipment consolidation,
  analytics APIs, and AI are not implemented by this change.
- The workflow is stored as an aggregate for a transactional first implementation.
  Reporting/indexes or normalized event/task tables should be designed around real
  query requirements; JSON storage is not a finished analytics architecture.

## Verification

`dotnet run --project tests/FreightWorkflowChecks` runs the domain checks. Set
`FREIGHT_CHECK_CONNECTION` to an administrator connection for an isolated localhost
database whose name starts with `aurora_workspace_checks_` to include database checks.
Use a schema-only copy with migration 0018 applied. The harness creates synthetic
tenants and switches to `aurora_app` for runtime operations, including RLS checks.
It never needs production data. 54 checks passed with database integration enabled,
including execution after dispatch and protection against forged history.

`tests/OrderWorkspaceChecks` passed 103 checks using `WORKSPACE_CHECK_CONNECTION` as
the runtime role on the same isolated database. Its overlapping-plan assertions were
updated to the existing planning-lock behavior: rejection occurs when creating the
second draft, not only when finishing it.

The API, client, and PlannerPreview build without warnings. PlannerPreview supports
workflow creation and execution in memory only, using synthetic orders. Its ordinary
order/manifest persistence remains disabled. Use the full application for authenticated
database verification.

Browser verification exercised plan creation, task requirements, monetary entry,
task completion with actual cost, a customer-caused failed delivery, and a separate
retry with the failed attempt preserved. Migration 0018 was also applied to the
local `127.0.0.1:55432/aurora_test` copy, and the local API/client were restarted.
No production deployment was performed.
