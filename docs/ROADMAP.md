# Roadmap

Future extensions, written down instead of built. Each says where it would plug in.

## Spell mana snapshot in reports

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

## Queue name from configuration

The queue name `crash-reports` is written in the `ServiceBusOutput` and `ServiceBusTrigger`
attributes. Binding expressions let it come from an app setting instead
(`"%CrashReportsQueue%"`), so separate environments (e.g. test and production) could use
different queues without code changes. Small; no new code beyond the attribute values.
