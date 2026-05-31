# Project Map

## Project North Star
[Populated by trunk discovery. The outcome this project is held to — specific about what "good" looks like, flexible about the how. Every Enuncia skill, on every branch, loads this at activation and checks its output against it before presenting.

Altitude rules for this section:
- Frame outcomes, not features. Don't name mechanisms, UI surfaces, or prescribed behaviors.
- Specific but flexible. Name the outcome, users, and constraints concretely; leave the how open.
- Must be authored once during trunk discovery. Branches revisit and may propose amendment but do not rewrite per-branch.]

## Branch Goals
[Populated by branch discovery. Each branch adds a subsection at the same altitude as the Project North Star, scoped to what that branch delivers. Does not replace the Project North Star.]

<!-- Per-branch subsections go here, e.g.:
### [branch-name]
[Branch-specific goal at the same altitude as the Project North Star.]
-->

## Overview
[Filled by compose — 2–3 sentences: what this project is, who it's for, how it's delivered. No tech here; that lives in Operational Foundation.]

## Operational Foundation
[Filled by design. Tech stack, deployment, auth pattern, repo structure, developer workflow. Each facet: short paragraph or bullet group. Point to code for anything that would otherwise be verbose (e.g., "Authoritative schema: `app/models/`"). Do not list class names, method signatures, enum values, or config keys here — those live in code.]

## Capabilities
[Filled by compose; refined by design. Organized by experience slice — this is the stakeholder-value spine of the map. Each slice is a subsection. A slice present in this section is understood to be part of the current product; no status tagging needed.]

<!-- Per-slice subsections go here, e.g.:
### [Slice name — stakeholder-facing phrasing]
- **What stakeholders can do:** [one sentence]
- **Components:** [Component A], [Component B]
- **Key connections:** [concise flow — A→B summary, B→external summary]
-->

## Component Reference
[Filled by compose; refined by design. Flat lookup list — one sentence per component. No tech detail, no task IDs, no slice tags. If you're tempted to expand an entry, expand the slice narrative instead or point to code. A component present here is understood to be in use.]

<!-- Per-component entries, e.g.:
- **[Component name]** — [one sentence: what it does; where it lives in code if non-obvious]
-->

## Pointers
- Bugs → `docs/project_notes/bugs.md`
- Decisions → `docs/project_notes/decisions.md`
- Config, credentials, URLs → `docs/project_notes/key_facts.md`
- Work log → `docs/project_notes/issues.md`
- Map evolution by phase → `docs/project_notes/map_history.md`
