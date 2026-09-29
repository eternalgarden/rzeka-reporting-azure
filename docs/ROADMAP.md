# Roadmap

Future extensions, written down instead of built. Each says where it would plug in.

## Reading reports

### Read API

Today, reports can only be read by querying the database. A small **ASP.NET Core minimal API**
would list issues and their reports over HTTP, as a separate service reading the same
database. That keeps the event-driven write path (Functions run *because* a report or message
arrived, and nobody waits for the result) apart from the request-response read path (someone
asks and waits for the answer). It's the first candidate after this version, and what Eris
offline mode would talk to.

- **Where it plugs in:** a new project next to the Functions project, reading `Issues` and
  `CrashReports`.
- **Project split:** once two apps need the database code, move `Database/` out of the
  Functions project into its own project (e.g. `RzekaReporting.Data`), shared by both. Until
  then, one project is simpler, so it's deliberately not split yet.

### Eris: reports view

A first, simple view of stored reports in Eris, rzeka's browser debugger: a list of issues
(title, count, first and last seen), and for each report its stack trace. No causality graph
yet. It's the step between the read API and Eris offline mode.

- **Where it plugs in:** a new view in the Eris UI, fetching from the read API.
- **Why not straight from the database:** Eris runs in the browser, so it would need the
  database password in the page, where anyone could read it. The read API keeps it on the
  server. For ad-hoc SQL, the portal's Query editor or a desktop client already exist.

### Eris offline mode

Load a stored report into Eris and replay the causal story of a crash from someone else's
machine: the chain as a graph with the failing spell highlighted, the stack trace next to it,
and the breadcrumbs as a timeline. Values stay out (see ADR 0002); the structure is enough to
see *how* the failure came about.

- **Where it plugs in:** the report's `Payload` column already holds `chain` and
  `breadcrumbs`; Eris would fetch it from the read API.
- **Depends on:** the read API and a stable report schema.

### Aggregate views

Which spells fail most across installs, and how failures trend across app versions. Mostly
queries over the existing tables (`CrashReports.AppVersion`, `Issues.Count`), exposed by the
read API.

## The client (rzeka.Reporting)

### Offline buffering

Save reports to disk when they can't be sent (no network, the game closing, the player not
having answered consent yet) and send them later. Safe to resend because the report ID makes
duplicates harmless (ADR 0003).

- **Where it plugs in:** a file-backed queue in front of `ReportSender`; on start, reports
  left from the previous session are sent first.

### Process-level exceptions

Capture exceptions outside the river too: `AppDomain.UnhandledException` (a crash that ends
the process) and `TaskScheduler.UnobservedTaskException` (a failed task nobody awaited). There's
no failing spell then, but the **breadcrumbs** still show what the river was doing just before,
which a generic crash reporter can't.

- **Contract:** `failure.spell` and `triggers` become optional, so `schemaVersion` 2.
- Exceptions inside Godot's own callbacks (`_Process`, signals) are caught by Godot, not .NET,
  and need a Godot-side hook in the game itself. Godot 4 removed Godot 3's
  `GD.UnhandledException` (issue godotengine/godot#73515, still open). The likely replacement
  is the `Logger` class added in Godot 4.5: registered with `OS.add_logger()`, its `_log_error`
  receives every error the engine logs, with script backtraces. Needs testing whether caught
  C# exceptions arrive there; it's called from several threads, so it must be thread-safe.

### Owner labels for chain nodes

With `IncludeOwnerLabels = true`, only the failing spell gets its `describeOwner` label today.
Chain nodes could get theirs too, making the causal story easier to read.

### Allowlist of reportable fields

Let the developer mark specific matter properties as safe to report (e.g. an attribute on the
property), so harmless values like IDs or counters can appear in reports. An allowlist, not a
denylist: a forgotten field stays hidden instead of leaking (ADR 0002).

### Publish the package

`EternalGarden.Rzeka.Reporting` isn't on NuGet yet: rzeka's release workflow packs only core
and dev. Adding it there publishes it with the next release tag.

## Report content

### Spell mana snapshot in reports

When a spell fails, include which spells were alive and whether they had mana (and which
ingredient they were missing). The causal chain shows what *did* happen; a mana snapshot shows
what *could* happen at that moment. That's useful for bugs where something expected never
arrived, e.g. a Loom waiting on an ingredient whose Strand had already been disposed.

- **Where it plugs in:** the reporter tracks spell lifecycle from Eris (`SpellOccurences`) the
  same way `RiverMemory` tracks matter, and adds the snapshot in `CrashReportBuilder`.
- **Size:** only spells connected to the failing one (its ingredient providers and output
  consumers) plus a count of the rest, to stay within the 64 KB limit.
- **Contract:** a new part of the report, so `schemaVersion` 2.
- Pairs with Eris offline mode, which could show it as the river's state at the moment of the
  crash.

### Report Horrors, not only failures

Horrors are rzeka's "this should never happen" messages that *didn't* crash anything: its own
diagnostics (matter published off the main thread, a Loom not wired to its input, a Shuttle
response without its request) and the developer's own `Whisper(…, RzekaMessageType.Horror)`
calls. From players' machines they'd be early warnings of bugs that quietly corrupt behaviour.

What it needs:

- **Causal chain:** `Whisper` already receives the live matter (`params IMatter[]
  circumstances`); only the stored Horror message keeps just their IDs. rzeka would publish the
  matter along with the Horror, the same way the error boundary publishes a Miscast, so the
  chain can be walked back like for failures.
- **Message templates:** a Horror's text is written by the developer and may contain values
  (`$"Entry {entry.Title} missing"`). A template overload, as in structured logging
  (`Whisper("Entry {Title} missing", entry.Title)`), would send only the template, which holds
  no values, and keep the values on the machine. Plain-text Horrors would be scrubbed like
  exception messages.
- **Grouping:** by the template, so every "Entry … missing" Horror becomes one issue, whatever
  the entry.
- **Contract:** a report without an exception, so `schemaVersion` 2.

## Grouping

### Regrouping after rule changes

When the grouping rules change, the version prefix changes (`v1|` → `v2|`), so new reports get
new fingerprints while old ones stay in their old issues. Since every report keeps its stack
trace and fields, a one-off job could recompute the old reports with the new rules and move
them to their new issues, so all history is grouped the same way.

## Security and operations

### Rate limiting and key rotation

Ingest is a public endpoint (the function key can be extracted from any game build). Rate
limiting in front of it (Azure API Management or Front Door) would stop floods.

Rotating the key (replacing it with a new one) would cut off an abused key, but every
installed build still has the old one, so it's a last resort. Functions allow several keys at
once, so the smooth way is: add a new key, ship a build with it, and remove the old key once
most players have updated.

### Managed identity

Replace the connection strings for Service Bus and Azure SQL with the Function App's own
identity: the app proves who it is to Azure, and no key or password exists at all. The
`Connection` settings then hold only addresses.

### Private network access to the database

Today the SQL firewall allows "Azure services" (broad) plus one home IP address. A virtual
network with a private endpoint would make the database reachable only from inside, with no
public firewall. A Tailscale node inside that network would give the developer's laptop access
without per-IP rules.

### Retention and deletion

A policy for how long reports are kept, and a way to delete them: a timer-triggered function
removing reports older than a set age, and deleting an issue's reports on request.

### Queue name from configuration

The queue name `crash-reports` is written in the `ServiceBusOutput` and `ServiceBusTrigger`
attributes. Binding expressions let it come from an app setting instead
(`"%CrashReportsQueue%"`), so separate environments (e.g. test and production) could use
different queues without code changes. Small; no new code beyond the attribute values.

## Infrastructure and development

### Infrastructure as code and automated deployment

All Azure resources were created by hand with the `az` CLI. Describing them in **Bicep** (or
Terraform) and deploying from **GitHub Actions** would make the whole path from commit to
running system reproducible.

### Docker, for reasons inside this system

- **Local environment:** a `docker-compose.yml` starting SQL Server, Azurite and the Service Bus
  emulator, so the whole pipeline runs locally without Azure.
- **Integration tests with Testcontainers:** the database tests run against SQLite in memory,
  a different engine than Azure SQL. Testcontainers would run them against a real SQL Server
  in a container.
- **Optionally, hosting:** ingest and processor as containers on Azure Container Apps, scaling
  on queue length, if the hosting model ever needs to change.

Kubernetes is deliberately not on this list; it would only make sense with a real need.
