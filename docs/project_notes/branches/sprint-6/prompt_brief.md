# Prompt Brief: Ironhold Town — Tile Map Exploration & NPC Interaction

## Problem Statement
The Ancient Order frontend has HUD components (Sprint 5) but no world to explore. Players need a spatial town experience where they physically move through Ironhold, encounter NPCs, and trigger story events — bridging the backend narrative/dialogue systems to a visual, interactive frontend.

## Commander's Intent
**Desired end state:** A Pokémon Crystal/Blue-style top-down town where the player walks around Ironhold, approaches NPCs to talk, and story scenes trigger based on position and progression. The world feels like a place, not a menu.
**Non-negotiables:** Spatial movement (not click-to-advance), NPC interaction by proximity, dialogue overlay that preserves map context
**Boundaries:** React + TypeScript + Vite stack, must integrate with existing backend narrative/dialogue APIs, Equinox HUD stays visible during exploration

## Success Criteria
- Player moves a sprite through Ironhold town using keyboard controls (WASD/arrows)
- Town has distinct areas (arrival, market, gym) with collision boundaries
- NPCs are visible on the map and interactable when approached
- Dialogue overlay panel shows NPC text + personality-gated choices from backend
- Story events (3 Act 1 scenes) trigger based on position + progression flags
- Simple 16x16 or 32x32 pixel art for tiles, player sprite, and NPC sprites
- Equinox HUD integrates with the tile map view

## Scope
**In scope:**
- Tile map engine (canvas or CSS-based renderer)
- Player sprite with WASD/arrow movement + collision detection
- Town map data structure (Ironhold with 3+ distinct areas)
- NPC placement and proximity-based interaction trigger
- Dialogue overlay panel (portrait placeholder, text, choice buttons)
- Story event trigger system (position + progression markers)
- Wiring to backend narrative + dialogue API endpoints
- Simple pixel art assets (tiles, sprites)

**Out of scope:**
- Combat UI (Sprint 7)
- Multiple towns or areas beyond Ironhold
- Animated sprite sheets / walk cycles (static directional sprites OK)
- Audio/music
- Save/load UI

## Constraints
- React 19 + TypeScript 5.7 + Vite 6 (established in Sprint 5)
- Must use existing design token system (leather/metal aesthetic for UI chrome)
- Backend scene structure is linear (3 scenes, choice-driven progression)
- Backend dialogue trees are client-provided to the API

## Key Assumptions
| Assumption | Confidence | Basis |
|-----------|-----------|-------|
| Canvas-based rendering likely needed for tile map performance | Medium | CSS grid may be too slow for real-time sprite movement |
| Map layout defined in JSON data files | High | Matches existing scene/fixture data pattern |
| Backend may need minor additions for position-based event triggers | Medium | Current backend is purely choice-driven, not position-aware |
| 16x16 tile size gives the right retro Pokémon aesthetic | Medium | Standard for GBC-era games, but 32x32 may feel better on modern screens |

## Open Questions for Planning
- Canvas vs CSS grid for tile rendering (performance vs React integration trade-offs)
- How to bridge backend's choice-driven scene model with position-based triggers
- Tile map editor workflow (hand-authored JSON vs tooling)
- How the Equinox HUD overlays on the tile map without obscuring gameplay
- Specific NPC roster and placement for Ironhold town areas
- Camera/viewport behavior (scrolling vs fixed screen-sized areas)

## Decision Posture
| Area | Posture | Notes |
|------|---------|-------|
| Tile engine approach (canvas vs CSS, game loop) | I decide | Full creative control |
| Map layout & town areas | I decide | Full creative control |
| Pixel art style & tile size | I decide | Full creative control |
| Sprite design (player, NPCs, buildings) | I decide | Full creative control |
| Dialogue overlay UI design | I decide | Full creative control |
| Event trigger system design | I decide | Full creative control |
| HUD integration with game world | I decide | Full creative control |
| Backend modifications for position triggers | I decide | Full creative control |
| Camera/viewport behavior | I decide | Full creative control |
| NPC roster & placement | I decide | Full creative control |
