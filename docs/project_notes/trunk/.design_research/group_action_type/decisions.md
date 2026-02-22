# Design Decisions: Group Action Type

## Decision Log

**D1 — Leader-initiated, override model**
Leader declares GROUP; all non-KO'd allies are conscripted (individual declarations discarded). Chosen for simplicity and Skies of Arcadia alignment.

**D2 — Full trio, single target (POC)**
All allies pulled in, one enemy target. Future GROUP varieties can define different compositions and targets.

**D3 — Priority 0 (highest)**
GROUP resolves before all other actions, including DEFEND. Plan originally had GROUP at priority 2; revised during design. Priority table: GROUP=0, DEFEND=1, ATTACK/SPECIAL=2, EVADE=3.

**D4 — Block-only defense suppression**
Target forced to Block only. No Dodge, no Parry, no counter chains. Coordinated assault overwhelms individual defensive skills.

**D5 — DEFEND intercept immunity**
GROUP resolves before DEFEND; interception is impossible by design.

**D6 — 1.5x flat damage multiplier**
Applies to sum of all participants' damage. Designed as configurable (`GroupActionConfig.damageMultiplier`) for future extensibility.

**D7 — Energy gate: full segments required**
All participants must have full energy at declaration (Phase 3). GROUP consumes all energy on execution. Creates fighting-game combo arc: build energy over rounds, then unleash GROUP.

**D8 — Validate at declaration only**
Energy check at Phase 3. The only runtime failure is opposing GROUP going first and KO-ing an ally.

**D9 — GROUP fires with remaining participants**
If opposing GROUP KO's ally(ies) before your GROUP resolves, GROUP fires with whoever is non-KO'd. Multiplier unchanged. Even a solo GROUP fires.

**D10 — Opposing GROUP tie-break: team average speed**
When both teams GROUP simultaneously at priority 0, higher team average speed (non-KO'd members) resolves first.
