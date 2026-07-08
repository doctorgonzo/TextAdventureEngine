# Changelog

All notable changes to the Text Adventure Engine. Versioning follows [semver](https://semver.org): breaking save-format or API changes bump the major version once 1.0 ships.

## [0.9.0] — 2026-07-08

First store-readiness release. Everything below is relative to the original prototype.

### Added
- **Dialogue Graph Editor** — visual dialogue-tree authoring: inline text editing, one-click response creation, drag-to-link response/failure ports, entry-node badges per character, auto-arrange, full Undo.
- **Create Game Scene** (`Tools ▸ Text Engine`) — one-click fully wired playable scene.
- **Content Validator** (`Tools ▸ Text Engine`) — severity-ranked audit of every content asset with click-to-ping.
- **Connect Four difficulty AI** — Easy/Medium/Hard per NPC on the Character asset; Hard runs 8-ply minimax with alpha-beta pruning.
- **EditMode test suite** (`TextEngine.Tests`) — minigame AI, noun matcher, catalog integrity, save round-tripping.
- **Save-format versioning**, quest/status-effect/name/drunkenness persistence, and resilient loading with warnings instead of crashes.
- **Parser upgrades** — article stripping, direction shortcuts, partial matching with disambiguation, fully data-driven verbs (CustomActions work standalone).
- **Equipment bonuses** — weapon/armor attack and attribute bonuses now apply, recalculated on equip/unequip.
- **Input System compatibility** — works under the legacy Input Manager or the Input System package, detected at compile time.
- **Configurable content root** — editor tools create assets where EngineSettings points.
- Startup scene-wiring validation with actionable `[Text Engine]` errors; `verboseLogging` toggle.

### Changed
- All code namespaced (`TextEngine` / `TextEngine.EditorTools`) with `TextEngine.Runtime` / `TextEngine.Editor` asmdefs.
- Package restructured under a single root: `Assets/TextAdventureEngine/{Runtime, Editor, Demo, Documentation, Tests}`.
- All content consolidated under `Demo/Resources` so runtime catalogs are complete.
- Rooms, shops, inventory, and equipment hold `ItemInstance` wrappers — ScriptableObject assets are never mutated at runtime.
- Interactable save state keyed by location + asset name (collision-proof).
- World Map editor: exits retarget in place preserving configuration; Undo supported everywhere.
- Zero-warning compile on Unity 6000.5.

### Fixed
- Weapons previously contributed no damage; debuff abilities applied twice; stun didn't block spellcasting; status effects ticked twice during combat.
- Loading a save restored almost no items (catalog gap); quests, player name, and active effects weren't saved at all.
- Dialogue nodes with no responses soft-locked the game; out-of-range quest objective indices crashed conversations.
- Duplicating an exit in the Location inspector silently cloned InstaDeath/lock configuration.
- Scenario Loader's "auto-press space" printed a parse error into the game.
- Flag dropdowns could silently rewrite values missing from the registry.

### Removed
- Vendored Asset Store Tools package (761 files), unlicensed demo music, drug-themed demo items.
