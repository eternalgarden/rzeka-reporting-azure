# 1. Queue between ingest and processing

- **Status:** accepted
- **Date:** 2026-09-24

## Context

Crash reports arrive over the internet from the game (rzeka.Reporting running inside it).
Turning a report into database rows means parsing it, computing its fingerprint and writing to
Azure SQL, and any of that can be slow or fail:

- the database can be briefly unavailable. The serverless database sleeps when idle, and the
  first connection after that fails with Azure SQL error 40613 ("database not currently
  available") while it wakes up;
- a report can be malformed in a way that is only noticed during full parsing.

The game must never wait on any of this, and a report should not be lost just because the
database was busy at that moment.

## Decision

Split the work into two Azure Functions connected by a Service Bus queue (`crash-reports`):

- **Ingest** (HTTP trigger) checks only the envelope (size, JSON, `schemaVersion`, `reportId`),
  puts the report on the queue and answers `202 Accepted` immediately.
- **Process** (Service Bus trigger) parses the report, computes the fingerprint and stores it.
  A malformed report is dead-lettered with the reason. Any other failure (database asleep or
  unreachable, a timeout, a bug in our code) is thrown, so the message comes back when its lock
  expires and is retried. After 10 deliveries Service Bus dead-letters it itself
  (`MaxDeliveryCountExceeded`). Lock duration (1 minute) and maximum deliveries (10) are queue
  settings, left at Service Bus's defaults.

## Alternatives considered

- **Ingest writes straight to the database.** Simpler, one function and one resource less.
  Rejected: a slow or sleeping database would make the game wait or lose reports, and there
  would be no retries or dead-lettering without writing them by hand. The queue doesn't win
  by being faster than the database, but by being always there to accept a report: Service Bus
  never sleeps, while the database can be asleep or unreachable.
- **Azure Storage Queues instead of Service Bus.** Cheaper and simpler. Rejected: fewer
  features (dead-lettering with reasons, delivery counts), and Service Bus is the tool this
  project is meant to learn.

## Consequences

Pros:
- If Process is failing or slow, Ingest keeps accepting reports, and each can be scaled or
  changed on its own. The only thing they have in common is the queue.
- Processing scales by itself: the runtime takes messages in batches and runs Process for
  several in parallel, and under more load Azure adds instances. They compete for the same
  queue, and each message goes to exactly one of them.
- Once Ingest has answered `202`, the report is stored in Service Bus and survives any
  problem with the database. (If Ingest itself can't be reached, the game's send fails and the
  report is lost: the client doesn't buffer reports on disk yet; see the roadmap.)
- Retries, delivery counts and the dead-letter queue come from Service Bus, not our code.
- The game gets an answer in milliseconds, whatever state the database is in.

Cons:
- One more resource to run and pay for (Basic tier: cents).
- Reports reach the database a few seconds after they are sent.
- Validation happens in two stages. A report with a valid envelope but missing fields gets
  `202` from Ingest and is only rejected later, into the dead-letter queue, so the game never
  learns about it.
