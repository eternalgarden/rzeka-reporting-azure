# 3. Client-generated report ID

- **Status:** accepted
- **Date:** 2026-09-24

## Context

The same report can reach the processor more than once:

- Service Bus delivers **at least once**. If Process stores a report but fails before
  completing the message (a crash, a timeout, the lock running out), the message comes back
  and is processed again.
- Later, the game may resend reports itself: a future version could save reports while
  offline and send them afterwards (see the roadmap). If a send arrives but its `202` answer is
  lost on the way back, the game would send the same report again.

A duplicate must not create a second report row, and it must not count the same failure twice
in its issue.

## Decision

The **client** (rzeka.Reporting, inside the game) gives every report a new GUID, `reportId`,
once, when the failure happens. The ID travels with the report and is the primary key of the
`CrashReports` table. A duplicate is therefore never a second report with a new ID, but the
same report, with the same ID, arriving again.

Process checks for the ID before saving. If the report is already stored, the message is
simply completed (`Duplicate`). Otherwise, the report is inserted and its issue's count is
increased **in one transaction**, so either both happen or neither does.

## Alternatives considered

- **Let the database generate the ID** (an auto-increment key). Rejected: every delivery would
  look like a new report, so duplicates couldn't be recognised at all.
- **Service Bus duplicate detection.** Rejected: it needs the Standard tier (we use Basic),
  only covers a limited time window, and wouldn't help with a message delivered again after it
  was already stored.
- **Derive the ID from the report's content** (a hash). Rejected: two genuine occurrences of
  the same bug can look identical, and they must count as two. Such a hash does exist in this
  project, the fingerprint, but it answers a different question: it identifies the *bug* and
  groups occurrences into an issue, while the report ID identifies one *occurrence* of that bug.

## Consequences

Pros:
- Duplicates are harmless, whatever their cause: Service Bus redelivery now, game resends
  later.
- Processing is **idempotent**: handling the same message twice has the same result as
  handling it once. That's what makes "throw and let Service Bus retry" in ADR 1 safe; without
  the ID check, a retry after a successful save would store the report twice and count the bug
  twice.
- The ID exists from the moment the failure happens, so client logs and database rows can be
  matched.

Cons:
- The server trusts the client to make IDs unique. Random GUIDs make accidental collisions
  practically impossible.
- If two deliveries of the same report are processed at the same moment, both pass the check,
  and the second insert fails on the primary key. That failure is thrown, the message comes
  back a minute later, and is then recognised as a duplicate: correct, only slower.
