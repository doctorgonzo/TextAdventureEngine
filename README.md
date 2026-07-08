# Text Adventure Engine

A data-driven engine for building parser-style text adventure games in **Unity 6 (6000.5.1f1)**. Rooms, items, enemies, dialogue, quests, and shops are all authored as ScriptableObject assets — no code required to build a game — while the runtime handles parsing, combat, conversation, progression, and saving.

The player types natural commands (`go north`, `take lantern`, `use key on door`, `attack goblin`, `talk to merchant`) into a terminal-style UI with a typewriter effect, colorized output, and live keyword highlighting.

---

## Features

### World & exploration
- **Locations** with rich descriptions, connected by **exits** in six directions (N/E/S/W/Up/Down).
- Exits can be **locked** (requiring a specific key item), **hidden** (revealed by events), **item-blocked**, or trigger special actions (instant death, stripping equipment, revealing items in another room).
- **Interactables** — objects with state machines you can `push`, `pull`, `flush`, `activate`, or use items on, producing effects like revealing exits, spawning items, setting flags, or changing their own state.
- **Items** with types (weapon, armor, consumable, currency, key/quest) plus in-room and inventory descriptions.

### Combat
- Turn-based combat with **hit / dodge chance**, damage variance, and armor mitigation.
- **Enemy AI** driven by `EnemyBehavior` assets: prioritized actions gated by conditions (e.g. "if my health < 30%, heal").
- **Special abilities** — self-heal, stun, mana drain, cleanse, and applying buffs/debuffs.
- **Status effects** (damage/heal-over-time, attack/defense buffs and debuffs) that tick in real time.
- **Ambushes** from enemies that attack on sight, **loot drops**, and **XP rewards** on defeat.

### Character progression
- Optional **leveling system** with scaling XP curves and skill points.
- Optional **primary attributes** (Strength, Agility, Stamina, Intellect) that derive secondary stats (health, mana, attack, hit/dodge chance).
- **Skills**, both active (mana-cost spells targeting enemies or self) and passive (permanent attribute bonuses), learned with skill points or purchased from **skill trainers**.

### NPCs, dialogue & quests
- **Branching dialogue trees** with numbered player responses.
- Nodes can require **world flags** or **items**, branch to failure nodes, and fire actions: give/take items, reveal exits, start a minigame, turn an NPC hostile, open a shop or trainer, and start/update/complete quests.
- **Quests** with objectives and currency / item / XP rewards.
- **Shops** with buy/sell using an in-game currency.

### Presentation & UX
- Typewriter text rendering with a queue, **press-space-to-skip**, and auto-scroll.
- Color-coded output (player input, game responses, keywords, enemies) and automatic **keyword highlighting** of things in the current room.
- **Command history** (up/down arrows) and a configurable characters-per-second speed.
- Optional flavor systems, e.g. a **drunkenness** system that literally makes the on-screen text wobble.

### Save / load
- Full game state serialized to JSON in Unity's persistent data path — player stats, inventory, equipment, learned skills, world flags, and the runtime state of every room (items, enemies, characters, interactables, exits, shop stock).

### Extras
- A built-in **Connect Four minigame** as an example of an alternate game mode reachable from dialogue.

---

## Editor tools

Custom Unity editor windows make authoring content faster:

- **World Map Graph Editor** (`Window ▸ World Map Graph Editor`) — a visual node graph of every location. Drag from a room's compass port into another room's *Entrance* to create an exit; the reciprocal exit is auto-created but independently deletable (so one-way passages are easy). Right-drag to pan, `Ctrl+A` to auto-arrange, toolbar buttons to create locations.
- **Flag Inspector** (`Window ▸ Flag Inspector`) — view and toggle all world flags at runtime for testing.
- **Scenario Loader** — jump the running game to a defined starting scenario.
- Custom inspectors for characters, dialogue nodes, enemies, and locations.

---

## Architecture

The runtime is organized around a central `GameController` with dedicated collaborators rather than one monolith:

| Piece | Responsibility |
|-------|----------------|
| `GameController.cs` | Lifecycle, input parsing, movement, inventory, shops, skills, orchestration |
| `GameController.Combat.cs` | Attack resolution, enemy AI, abilities, status effects (partial class) |
| `GameController.Dialogue.cs` | Conversation flow and dialogue-node actions (partial class) |
| `TextRenderer` | The queued typewriter output pipeline, colorization, and scrolling |
| `SaveSystem` | JSON serialization and disk I/O for saves |
| `WorldState` | Loaded content catalogs and all per-location runtime state + lookups |

Game content lives as ScriptableObjects under `Assets/Resources/` (`Locations/`, `Items/`, `Enemies/`, `Skills/`, `Actions/`, `Characters/`, `Scenarios/`, …) and is loaded at startup, so designers build and tweak games entirely from the Inspector.

---

## Getting started

1. Install **Unity 6000.5.1f1** (or a compatible Unity 6 release).
2. Clone the repo and open the project folder in Unity.
3. Open the main scene under `Assets/Scenes/` and press **Play**.
4. To build your own game, create Location / Item / Enemy / Skill / Dialogue assets under `Assets/Resources/`, wire rooms together in the **World Map Graph Editor**, and set the starting location on the `GameController` in the scene.

> Note: Unity-generated folders (`Library/`, `Temp/`, `Logs/`, builds) are intentionally excluded from version control and will be regenerated on first open.

---

## Command reference

Movement & world: `go <dir>` (with `n`/`s`/`e`/`w`/`u`/`d` shortcuts), `look [at <thing>]`, `take <item>`, `drop <item>`, `use <item> [on <target>]`, `push` / `pull` / `flush` / `activate <thing>`

The parser ignores articles (`take the lantern` works), accepts partial names (`attack gob` matches the goblin), and asks which one you meant when a partial name is ambiguous.

Character: `inventory`, `equip` / `unequip <item>`, `equipment`, `status`, `char`, `skills`, `learn <skill>`, `cast <skill> [on <target>]`

Combat & NPCs: `attack <enemy>`, `talk to <character>`

Economy & quests: `balance`, `buy <item>`, `sell <item>`, `quests`

System: `save`, `load`, `help`

Verbs are data-driven (each `Action` asset defines a keyword plus synonyms), and custom verbs can be added as `CustomAction` assets without touching code.
