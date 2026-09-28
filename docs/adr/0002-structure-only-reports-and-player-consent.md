# 2. Structure-only reports and player consent

- **Status:** accepted
- **Date:** 2026-09-25

## Context

rzeka's main user, sanctuary, is a journaling app. The matter flowing through its river can
contain what people write about their lives, and a crash report leaves their machine. Values
aren't the only way private data can leak:

- exception messages often quote their input (`Could not open 'Dear diary…'`);
- stack traces contain file paths with the user's name (`/home/anna/…`);
- owner labels (e.g. Godot node names) and an owner object's `ToString()` can contain anything.

## Decision

**Reports contain structure, never values.**

- Matter appears only as its type name, ID and links to its causes; spells as their title,
  school and owner *type*. Matter properties are never read.
- Exception messages are **scrubbed** (paths, quoted text, emails, URLs, long numbers
  removed), or omitted completely with `IncludeExceptionMessages = false`.
- Stack traces keep file names but lose their directories.
- Owner labels from `describeOwner` are only sent with `IncludeOwnerLabels = true`.
- Times are UTC (a local offset would reveal the timezone); the OS is sent as a family only
  (`Linux`), not a full version string.

**Nothing is sent without the player's consent.** Consent starts as `Unknown`: reports are
only held in memory. `Granted` sends them; `Denied` discards them and stops building new ones.

## Alternatives considered

- **Send matter values** (the data inside each matter). Much more useful for debugging, since
  the value is often the bug: a malformed note ID or an unexpected counter would show at once.
  Values range from harmless like these to highly personal, like a journal entry's title or
  text, and the reporter can't tell which is which without the developer saying so. Rejected
  as a default for that reason; the allowlist below is the way to get the harmless ones.
- **Send first, delete on the server if consent is missing.** A common pattern in analytics
  tools. Rejected: once data has left the machine, the promise is already broken, whatever the
  server does afterwards.
- **An allowlist of safe fields** (the developer marks specific matter properties as
  reportable). Kept for the roadmap: a good middle ground, but not needed for a first version.
  It should be an allowlist rather than a denylist (e.g. a `[Sensitive]` attribute): if someone
  forgets to mark a field, an allowlist hides it, while a denylist would leak it.

## Consequences

Pros:
- Safe by default: a developer has to opt *in* to anything more revealing.
- Checked on real output: the demo's reports contained no username, and a path in an
  exception message arrived as `<path>`.
- Easy to explain in a privacy policy: "which parts of the app failed, and in what order",
  never "what you wrote".

Cons:
- Less to debug with: when a bug depends on a specific value, the report can't show it.
- Scrubbing is best effort, not a guarantee. It errs towards removing too much (quoted
  parameter names disappear too), but unusual formats could still slip through; turning
  messages off is the safe option.
- Held reports live only in memory: if the game closes before the player answers, they are
  gone.
- The server necessarily sees the IP address of each request. Our code doesn't store it.
