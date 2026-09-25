# PTV meeting notes — September 22, 2026

Working notes from transcript chunks. Statements below are attributed meeting claims, not independently verified vendor behavior. Filler and meeting setup omitted. No final decisions yet.

## Chunk 1 — Performance and replanning

- Aurora supplied test files and corresponding outgoing PTV JSON to Bernd shortly before the meeting. PTV began reviewing them using a tool referred to as MIRA.
- Aurora's main questions: initial optimization of roughly 600 orders took longer than expected; can a partial reroute avoid recalculating all orders?
- Bryan described the implemented Quick update: retain the last successful result in browser memory, edit appointments/trucks/service times, and submit updated inputs plus previous route sequences with a five-second solver budget. He reported roughly 40–60 seconds for an initial run and 5–6 seconds for updates in his experience; these are not benchmarks or guarantees.
- Code clarification for future answers: Quick update still submits the current complete input plan plus seed routes in a NEW optimization. It is not a changes-only payload, resumed provider job, or proof that only affected routes are recalculated. Earlier conversational wording "instead of sending the whole plan" should not be carried forward as implementation fact.
- Bernd described a few hundred orders and several dozen vehicles as a medium planning scenario.
- Bernd explained starting an optimization returns a UUID; settings specify maximum optimization runtime (20 minutes was an example, not a recommendation adopted by Aurora).
- Bernd described polling that UUID every few seconds and said there are two approaches. First introduced: "get optimization progress," returning KPIs at different points in time. Second approach has not yet been explained in this chunk.
- Bernd began preparing a demonstration using Aurora's input. No demonstration result is present in this chunk.

## Open questions / follow-ups

1. PTV's recommended approach for partial replanning versus Aurora's current seeded Quick update.
2. Whether routes/orders can be fixed or excluded from recomputation; what data must be resubmitted.
3. Actual runtime/quality tradeoff for the approximately 600-order case; example 20-minute duration is not an agreed setting.
4. Exact progress endpoint/response, the second polling approach, and how interim results can be consumed.
5. Outcome of PTV's review of the supplied requests and planned demonstration.

No assigned deadlines or confirmed implementation changes captured yet.

## Chunk 2 — Interim results and graceful stop

This paste repeats chunk 1; only new content is recorded here.

- Bernd described two polling choices: progress returns KPI history (routes, scheduled/unscheduled counts; chart discussed total cost), while optimization result can return full interim route structures during an active run.
- He described refreshing route geometry/assignments while optimization continues as an application option, not an Aurora feature already implemented.
- Bernd said settings.duration is mandatory and a large budget can run unnecessarily long even on small problems. Aurora should choose a stopping strategy rather than assuming the service automatically ends when additional time is unhelpful.
- Bernd described a graceful stop operation: request stop, wait for the next possible solver exit point, then collect the final result. This differs from Aurora's current browser cancellation, which aborts local HTTP work without sending provider stop.
- KPI samples in the demonstration were described as roughly five seconds apart; this is not a confirmed polling quota or guaranteed update interval.
- Transcript ends mid-question asking whether progress includes current cost. Await the answer; exact field, objective meaning and comparison semantics remain unconfirmed.
- No API operation URL, HTTP verb, terminal state semantics, billing rule, or agreed automatic stopping threshold supplied yet.

### Implementation questions to resolve

1. Exact stop operation and final status/result lifecycle: distinguish graceful stop retaining solution from cancellation/deletion; is a result returned as SUCCEEDED or another state?
2. Is every interim result a coherent feasible snapshot, and can it be absent before a first solution? Can reconstruction/interim results contain violations and how are these flagged?
3. Does stop retain the best solution found or the latest snapshot; what quality ordering does PTV use when coverage and cost trade off?
4. Recommended polling cadence, rate limits and billing for progress/result requests; progress history incremental versus full history each response.
5. Cost field question is already being asked in the meeting; capture answer before repeating it.

### Aurora implementation implications (our analysis, not meeting decisions)

- Current planner holds one browser-to-server request while the server polls and returns only when complete. Live progress needs a job lifecycle exposed to the UI (submit/status/progress/result/stop), with authenticated tenant ownership of provider IDs.
- Persist job ID/state if refresh/reconnect recovery is desired; browser-memory-only state is insufficient for durable recovery.
- Longer solver budgets require reviewing current 180-second poll timeout and HTTP/proxy timeouts; setting duration alone is insufficient.
- Label interim plans as provisional; final export/dispatch policy needs a product decision.
- Avoid calling paid road routing for every interim update without a deliberate refresh policy.

## Chunk 3 — Answers on progress, stopping, restrictions and replanning

Overlapping transcript deduplicated. The following records Bernd's explanations, not independently verified API documentation.

- Progress returns the full KPI history from the start of a job through the current time, with samples roughly every five seconds. The transcript does not name the exact cost metric/schema field shown on screen.
- Bernd recommended polling about every five seconds; more frequent requests likely provide no benefit. Billing/rate-limit treatment is still unanswered.
- Four exit strategies described: manual stop; a condition such as all orders scheduled; no improvement for a chosen period; or maximum duration reached. No specific automatic rule or threshold was selected for Aurora.
- Bernd said maximum duration can be up to a day subject to subscription access. He referenced an existing forum post on duration/exit strategies; URL was not included. He cautioned against deriving duration from a simple seconds-per-order formula.
- Get optimization result returns the best solution found according to the cost function, not an arbitrary latest candidate. Earlier solutions cannot be retrieved by paging backward. Graceful stop retains the best solution. Exact terminal status/stop endpoint details remain to be obtained.
- Restrictions clarification: interim/final plans consider configured constraints. Routing may permit restricted-road access depending on vehicle routing settings; Bernd described a setting to disallow routing violations, potentially leaving unreachable orders unscheduled. Do not equate a successful or stopped run with unconditional zero road violations.
- Manually supplied input routes can introduce other violations, such as overload or time-window conflicts. Bernd distinguished this from optimization without input routes. Aurora Quick update DOES supply input routes; its CLEANUP reconstruction policy is a separate setting from road-routing violation permission.
- Active jobs cannot be edited in place. Adding/removing trucks or adding orders requires a new planning job.
- Bernd explicitly described using output routes from a previous job as inputs to the new job. This supports Aurora's seeded replanning approach; it is not inherently a misuse or workaround, despite conversational wording calling it "hacky."
- For freight already staged at a ramp, preserve vehicle assignment by passing order-to-vehicle constraints into the next optimization. Previous routes alone do not imply assignment locks. Bernd began a demonstration; exact fields and example are still pending.

### Remaining implementation details after chunk 3

1. Exact cost/progress fields, stop operation and final status sequence; example requests/responses and forum link.
2. Exact routing-violation permission field/default, including consistent setup across OptiFlow and separate Routing API calls. This may relate to the demo warning but its actual cause remains unproven.
3. Exact order-to-vehicle locking fields; distinction between preserving assignment and preserving stop sequence; behavior for a removed/unavailable locked vehicle.
4. How to handle a job before its first usable solution and safely retrieve/stop the old job when launching a revised plan.
5. Polling billing/quotas and product choice of stopping criteria.

## Chunk 4 — Application-owned scenarios and incremental planning demo

Earlier content repeated; new material only:

- Bernd clarified that PTV does not remember the application scenario for a subsequent planning request. Aurora must submit a new complete JSON containing the revised order set and any input-route/assignment constraints. This does not negate retrieval of an existing job's result by UUID.
- Demonstration being prepared: add ten orders, retain assignments for freight ramp personnel have begun loading, and extend driver working time by one hour to explore capacity for the added work. These are demo assumptions, not approval to extend real shifts automatically.
- Periodic replanning (for example hourly) requires Aurora to decide which prior assignments become constraints for the next run.
- Exact changed request parameters have not yet been shown in the pasted transcript; continue tracking that example.

## Aurora product direction discussed alongside transcript

User prefers a wizard: available trucks -> appointments/service-time exceptions -> monitored optimization loop -> maps/results. Existing fleet should be the default; daily planning emphasizes availability rather than frequent equipment configuration. Detailed dimensions/capacities/costs belong primarily in the equipment catalog.

Assistant proposed retaining edits when moving between steps, a short adjust-and-rerun path from results, roughly five-second server-mediated progress polling, and a graceful stop-and-review action. These are design proposals; no UI changes have been implemented in this conversation. Automatic stop thresholds and acceptance/write-back behavior remain undecided.

## Chunk 5 — Assignment constraints clarified

Repeated earlier material omitted. Bernd clarified the distinction that MIRA's wording had obscured:

- Input routes reuse the quality/structure of a previous plan; supplying them does NOT itself freeze the stop sequence. Fixing a sequence requires additional constraints. Exact sequence-lock parameters still not demonstrated in the transcript.
- Vehicle assignment can be enforced through unique vehicle categories. Demonstrated concept: only vehicle 2601 has category PIN STR2601, and the previously assigned orders have a constraint requiring that vehicle category. Preserve existing categories and other equipment requirements. Exact JSON nesting should be taken from the shared example, not guessed from spoken shorthand.
- Bernd described two complementary pieces: prior output transformed into the input routes array, plus order/vehicle category constraints retaining assignments.
- Seven vehicles were used in the initial example; three unused vehicles had no prior assignments to pin.
- For this category constraint, Bernd said omitting violation costs makes it hard: a pinned order can only be served by its designated eligible vehicle. This guarantees eligibility, not that every pinned order remains schedulable after changes.
- Explicit high violation costs can instead make reassignment possible at a penalty, per Bernd. Aurora did not select this softer behavior.
- Aurora participant said rerouting loaded/loading freight is rare; emergency exceptions would be handled manually, with a possible recalculation afterward for costing. Preserve these assignments by default; do not infer approval for automatic reassignment.
- Bernd gave a warehouse example of stable ZIP-area-to-ramp assignments supporting efficient loading. This illustrates operational constraints; it is not a new Aurora requirement.

Implementation follow-ups: obtain actual JSON showing required vehicle categories and penalty placement; confirm what happens when pinned work becomes infeasible. Assignment pinning does not establish that a loaded route excludes new orders, that its sequence is fixed, or that dispatched/completed stops are handled; those are separate operational decisions.

## Chunk 6 — Demonstration results and meeting close

Repeated discussion and unrelated post-meeting conversation omitted.

- Bernd contrasted hard assignments with soft preferred-area/ramp assignments: penalties allow beneficial exceptions before loading. Lower penalties permit more reassignment. No penalty amounts were agreed for Aurora.
- Demonstrated initial plan: 100 orders, seven of ten trucks used. Revised example: 110 deliveries, eight trucks used; Bernd explained that the ten new orders went to a previously unused truck because the initial seven had little remaining capacity. These are demo observations, not reproducible acceptance criteria without the supplied artifacts.
- Bernd showed variants with assignment constraints, and with those constraints plus input routes. He described seven order/vehicle category constraints and preservation of the existing GENERAL_FREIGHT category alongside pin categories.
- The example had no location time slots, individual service durations, weight/volume/pallet dimensions, a common routing profile and AVERAGE traffic. Bernd explicitly observed that routing violations were allowed in the reviewed planning. Obtain that exact request to determine whether this was inherited, defaulted or added during the demo.
- Depot pickup/unloading sequence was discussed as LIFO for this one-depot example. Do not generalize this into an Aurora requirement or assume loaded-truck sequence is locked.
- Progress example showed total cost declining roughly from 6,400 to 6,200 after orders were scheduled early. Spoken percentages were approximate. This illustrates why "all orders scheduled" need not be the desired automatic stopping condition.
- Bernd's sample UI polled full results about every 3–5 seconds and refreshed map/chart. He mentioned a powerful API key; comparative performance with Aurora's subscription was not established.
- Bryan proposed a wizard for vehicles, appointments, monitored optimization and user acceptance. Discussion also requested what-if edits after seeing results, such as removing five orders or extending a driver's day, followed by another calculation.
- Aurora agreed to discuss the scope offline BEFORE a deep rework. A participant cautioned that the existing implementation may be closer than Bryan thought. Do not interpret the meeting as approval to rewrite the whole UI immediately.
- Potential follow-up with PTV after Aurora explores the changes was discussed, with next week mentioned tentatively; no confirmed appointment or deadline.
- No resolution of the separate Routing API / bird's-eye fallback warning appears in the transcript.

## Consolidated outcome

1. Keep existing import, constraints, result parsing and seeded rerun foundations. One-shot optimization is a valid pattern; interactive monitoring is an additional workflow, not proof the existing integration was misusing PTV.
2. Design a monitored planning session, approximately five-second polling, visible quality/coverage, graceful stop and final retrieval. Maximum budget remains required. Select manual/automatic exit policy deliberately.
3. New inputs require a new job. Aurora owns versioned scenario inputs and prior results; input routes reuse prior work. Hard category constraints can preserve existing truck assignments when needed.
4. Daily fleet use should focus on saved equipment and availability. Wizard direction is proposed; offline Aurora scope review is the next agreed step before implementation.
5. Preserve post-result what-if comparisons and rapid return to editing. Loaded/loading assignment protection should be explicit; sequence freezing, admitting new freight to loaded trucks and real shift extensions are separate decisions.

## Follow-up checklist (owners suggested, not assigned commitments)

| Follow-up | Suggested owner | Status |
|---|---|---|
| Offline workflow/scope discussion before substantial UI work | Aurora | Agreed next discussion; not yet completed |
| Obtain demonstrated original/revised request and response JSON, including category constraints and input routes | Aurora / PTV | Pending; snippets described verbally only |
| Obtain progress/stop examples, final-state semantics and exit-strategy forum link | Aurora / PTV | Pending; behavior described, exact contract not yet captured |
| Diagnose Routing API violated=true and align actual vehicle/routing settings with OptiFlow | Aurora; PTV if needed | Unresolved; allowed violations observed in demo, cause not proven |
| Confirm polling charges/limits and subscription/runtime differences | Aurora / PTV | Unanswered |
| Decide auto-stop, scenario persistence, assignment protection and accept/export workflow | Aurora | Design decisions pending |

No code changes, deployment, external messages, or new paid optimization runs were performed as part of transcript capture.
