# Text Adventure Engine

A data-driven engine for building parser-style text adventure games in **Unity 6 (6000.5.1f1)**. Rooms, items, enemies, dialogue, quests, and shops are all authored as ScriptableObject assets — no code required to build a game — while the runtime handles parsing, combat, conversation, progression, and saving.

The player types natural commands (`go north`, `take the lantern`, `use key on door`, `attack goblin`, `talk to merchant`) into a terminal-style UI with a typewriter effect, colorized output, and live keyword highlighting.

📖 **[Full manual](Assets/TextAdventureEngine/Documentation/Manual.md)** · 📋 **[Changelog](CHANGELOG.md)**

---

## Features

### Parser
- **Natural input**: articles (`a`, `an`, `the`) are ignored, so `take the lantern` and `take lantern` both work.
- **Partial matching**: noun lookups resolve exact → prefix → substring, so `attack gob` hits the goblin. When a partial name is ambiguous, the game asks *"Which do you mean: X, Y?"*.
- **Direction shortcuts**: `go n` / `s` / `e` / `w` / `u` / `d` expand to the full compass directions.
- **Data-driven verbs**: every built-in verb is an `Action` asset defining a keyword plus synonyms. New verbs are added as assets — `CustomAction` for world interactions (give/take items, set flags, change interactable state, heal/damage, teleport, destroy the target, play a sound) and `CombatAction` for attack verbs — without touching code.
- **Command history**: up/down arrows recall previous commands.

### World & exploration
- **Locations** with rich descriptions, connected by **exits** in six directions (N/E/S/W/Up/Down).
- Exits can be **locked** (requiring a specific key item), **hidden** (revealed by events), **item-blocked** (impassable while carrying a specific item), or trigger special actions: instant death, stripping all equipment, or revealing items in another room.
- **Interactables** — objects with state machines you can `push`, `pull`, `flush`, `activate`, or use items on, producing effects like revealing exits, spawning items, setting flags, or changing their own state.
- **Items** with types (weapon, armor, consumable, currency, key/quest/general), stack limits, buy/sell prices, and separate in-room and inventory descriptions.

### Combat
- Turn-based combat with **hit / dodge / evasion chance**, damage variance, and armor mitigation.
- **Equipment matters**: weapons and armor contribute flat attack and attribute bonuses (strength/agility/stamina), recalculated on every equip/unequip.
- **Authorable attack verbs**: `CombatAction` assets define new attacks (backstab, power attack, wild swing…) with a damage multiplier, flat bonus, and accuracy modifier — plus targeting rules: provoke a friendly NPC into its hostile form, restrict the attack to targets unaware of you (stealth openers), or skip the enemy's retaliation for hit-and-run strikes.
- **Enemy AI** driven by `EnemyBehavior` assets: prioritized actions gated by conditions (e.g. "if my health < 30%, heal").
- **Special abilities** — self-heal, life drain, stun (blocks both melee and spellcasting for a turn), mana drain, turn-skipping taunts, and applying debuffs to the player.
- **Status effects** (damage/heal-over-time, attack/defense buffs and debuffs) that tick in real time and persist through saves.
- **Ambushes** from enemies that attack on sight (on every kind of exit), **loot drops**, and **XP rewards** on defeat.

### Character progression
- Optional **leveling system** with scaling XP curves and skill points.
- Optional **primary attributes** (Strength, Agility, Stamina, Intellect) that derive secondary stats (health, mana, attack, hit/dodge chance). With attributes off, simple flat stats from the engine settings are used instead.
- **Skills**, both active (mana-cost spells targeting enemies or self) and passive (permanent attribute bonuses), learned with skill points or purchased from **skill trainers**.

### NPCs, dialogue & quests
- **Branching dialogue trees** with numbered player responses; nodes with no responses end the conversation cleanly.
- Nodes can require **world flags** or **items**, branch to failure nodes, and fire actions: give/take items, reveal exits, start a minigame, turn an NPC hostile, open a shop or trainer, and start/update/complete quests.
- **Quests** with per-objective progress and currency / item / XP rewards.
- **Shops** with buy/sell using an in-game currency, per-shop runtime inventories.

### Presentation & UX
- Typewriter text rendering with a queue, **press-space-to-skip**, and auto-scroll.
- Color-coded output (player input, game responses, keywords, enemies) and automatic **keyword highlighting** of things in the current room.
- Optional flavor systems, e.g. a **drunkenness** system that literally makes the on-screen text wobble and sobers off in real time.
- **Audio hooks** — per-location background music, item pickup sounds, and `PlaySound` effects on interactions and custom verbs, all routed through a scene `SoundManager`.

### Save / load
Full game state serialized to JSON in Unity's persistent data path:
- Player stats, name, current health/mana, drunkenness, inventory, equipment, learned skills.
- **Active quests with per-objective progress** and completed quests.
- **Active status effects with their remaining durations.**
- The runtime state of every room — items, enemies (with current health), characters, interactables (keyed by location + asset name, so identically-named objects in different rooms stay independent), exits (locked/hidden), and shop stock.
- World flags.
- Loading is resilient: missing assets are skipped with warnings, and a save pointing at a deleted location falls back to the main start location.

### Connect Four minigame
A built-in example of an alternate game mode reachable from dialogue, played against whichever character started it. The AI difficulty is set **per NPC** on the `Character` asset, so some characters offer casual games and others are nearly unbeatable:

| Difficulty | Behavior |
|---|---|
| **Easy** | Picks random valid columns. |
| **Medium** | Takes an immediate win, blocks yours, otherwise plays center-biased random moves. |
| **Hard** | Minimax with alpha-beta pruning, 8 plies deep, window-scoring heuristic, center-out move ordering. Plays almost perfectly (sweeps Easy and Medium in testing) and still blocks threats even in lost positions. |

---

## Getting started

1. Install **Unity 6000.5.1f1** (or a compatible Unity 6 release).
2. Clone the repo and open the project folder in Unity. (Unity-generated folders — `Library/`, `Temp/`, `Logs/`, builds — are excluded from version control and regenerate on first open.)
3. Open the main scene under `Assets/Scenes/` and press **Play**.
4. To build your own game: run **`Tools ▸ Text Engine ▸ Create Game Scene`** — it builds a complete, wired game scene (canvas, scrolling output, input field, EventSystem for whichever input backend is active, GameController, SoundManager) in one click. Then create content assets in a `Resources/` folder (see the authoring table below), wire rooms together in the **World Map Graph Editor**, and point the starting locations at your own rooms.

---

## Command reference

| Category | Commands |
|---|---|
| Movement & world | `go <dir>` (or `n`/`s`/`e`/`w`/`u`/`d`), `look [at <thing>]`, `take <item>`, `drop <item>`, `use <item> [on <target>]`, `push` / `pull` / `flush` / `activate <thing>` |
| Character | `inventory`, `equip` / `unequip <item>`, `equipment`, `status`, `char`, `skills [list]`, `learn <skill>`, `cast <skill> [on <target>]` |
| Combat & NPCs | `attack <enemy>`, any `CombatAction` verbs your game defines (e.g. `backstab <enemy>`), `talk to <character>` |
| Economy & quests | `balance`, `buy <item>`, `sell <item>`, `quests` |
| System | `save`, `load`, `help` |

Inside a shop: `buy <item>`, `sell <item>`, `leave`. With a trainer: `buy <skill>`, `leave`. In the minigame: a column number `1`–`7`, or `quit`.

---

## Content authoring

**All game content lives inside a `Resources/` folder** using the subfolder names below, and is loaded into catalogs at startup — this single content home is what makes save/load restore-by-name work. The demo game's content is under `Assets/TextAdventureEngine/Demo/Resources/`; your own game's content can live in any `Resources/` folder with the same subfolder layout. Create assets via the `Assets ▸ Create ▸ Text Adventure` menu:

| Asset type | Folder | What it defines |
|---|---|---|
| **Location** | `Resources/Locations/` | Description, exits, default items/enemies/characters/interactables, shop settings, background music |
| **Item** | `Resources/Items/` | Type, descriptions, equipment bonuses (attack/defense/attributes), consumable effects, status effect on use, prices, stacking rules |
| **Enemy** | `Resources/Enemies/` | Stats, evasion, AI behavior, attacks-on-sight, loot, XP, exit revealed on death |
| **Enemy Behavior** | `Resources/Enemy Behaviors/` | Prioritized list of AI actions, each gated by conditions (own/player health thresholds) |
| **Special Ability** | `Resources/Special Abilities/` | Enemy ability: skip turn, heal, life drain, stun, mana drain, debuff, with magnitude and status effect payload |
| **Character** | `Resources/Characters/` | Name, descriptions, starting dialogue node, hostile form (enemy), skills taught, Connect Four skill |
| **Dialogue Node** | `Resources/Dialogue/` | Text, player responses (each linking to a next node), required flags/items, failure node, actions on success |
| **Quest** | `Resources/Quests/` | Name, description, objectives, currency/item/XP rewards |
| **Skill** | `Resources/Skills/` | Passive (attribute bonus) or active (mana cost, target type, effect list), level requirement, point/coin cost |
| **Status Effect** | `Resources/Status Effects/` | Effect type, magnitude, duration, tick interval, application chance and messages |
| **Interactable** | `Resources/Interactables/` | Noun, descriptions, initial state, interactions (verb + required state/item → effects), allowed custom actions |
| **Action** | `Resources/Actions/` | A built-in verb keyword and its synonyms |
| **Custom Action** | `Resources/Actions/Custom/` | A brand-new verb: keyword, synonyms, failure message, effect list |
| **Combat Action** | `Resources/Actions/Combat/` | A brand-new attack verb: damage multiplier/bonus, accuracy modifier, NPC provocation, stealth-only targeting, retaliation toggle |
| **Scenario** | `Resources/Scenarios/` | A test setup: starting location, inventory, enemies — jump there at runtime via the Scenario Loader |
| **Flag Registry** | `Resources/` | The master list of world-flag names, used to populate dropdowns in custom inspectors |
| **Engine Settings** | `Resources/` | The master feature toggles and balance numbers (below) |

> **Save-compatibility note:** saves reference content by asset name. Renaming or deleting an asset orphans that piece of state in old saves (it is skipped with a warning, never a crash).

### Engine settings

One `EngineSettings` asset, assigned to the `GameController`, controls:

- **Feature toggles** — leveling system, primary attributes, drunkenness, status effects. Systems that are off cost nothing and hide their output.
- **Content root folder** — where the editor tools create new content assets, so your game's content lives in its own `Resources/` folder, cleanly separated from the demo's.
- **Simple-stats balance** — flat max health / attack / hit / dodge used when primary attributes are off.
- **Attribute-derived balance** — base values and per-point multipliers for health (stamina), mana (intellect), attack (strength), hit/dodge (agility), plus caps.
- **Leveling balance** — XP curve multiplier, attribute and skill points granted per level.

---

## Editor tools

- **Create Game Scene** (`Tools ▸ Text Engine ▸ Create Game Scene`) — one-click scene setup: builds and wires the full UI + engine rig so a fresh project is playable immediately.
- **Content Validator** (`Tools ▸ Text Engine ▸ Validate Content`) — one-click audit of every content asset: broken exit destinations, locked doors without keys, dialogue actions missing their item/quest, out-of-range quest objectives, flags missing from the registry, assets outside `Resources/` (invisible to the engine), and duplicate asset names (which collide in the name-keyed save system). Results are ranked by severity with click-to-ping.
- **World Map Graph Editor** (`Window ▸ World Map Graph Editor`) — a visual node graph of every location. Drag from a room's compass port into another room's *Entrance* to create an exit; the reciprocal exit is auto-created but independently deletable (so one-way passages are easy). Right-drag to pan, `Ctrl+A` to auto-arrange, toolbar buttons to create locations.
- **Dialogue Graph Editor** (`Window ▸ Dialogue Graph Editor`) — branching conversations on a canvas: every dialogue node as a graph node with **inline text editing**, one-click "+ Response" authoring, drag-to-link response and failure ports, entry-node badges showing which character starts there, per-conversation auto-arrange, and full Undo.
- **Flag Inspector** (`Window ▸ Flag Inspector`) — view and toggle all world flags live at runtime for testing.
- **Scenario Loader** (`Window ▸ Scenario Loader`) — jump the running game to a defined starting scenario.
- **Custom inspectors** for characters, enemies, locations, and dialogue nodes — the dialogue inspector offers flag dropdowns (fed by the Flag Registry) and a one-click *"Create & Link New Node"* button for building trees fast.

---

## Architecture

The runtime is organized around a central `GameController` with dedicated collaborators rather than one monolith:

| Piece | Responsibility |
|---|---|
| `GameController.cs` | Lifecycle, input parsing & verb dispatch, movement, inventory, shops, skills, save/load orchestration |
| `GameController.Combat.cs` | Attack resolution, enemy AI, special abilities, status effects *(partial class)* |
| `GameController.Dialogue.cs` | Conversation flow and dialogue-node actions *(partial class)* |
| `GameController.ConnectFour.cs` | The minigame: board state, input, and the three-difficulty AI *(partial class)* |
| `TextRenderer` | The queued typewriter output pipeline, colorization, and scrolling |
| `SaveSystem` | JSON serialization and disk I/O for saves |
| `WorldState` | Loaded content catalogs, all per-location runtime state, and lookups |

Combat, dialogue, and the minigame are partials (rather than standalone classes) because they mutate a large amount of shared player state; splitting the files separates the concerns without threading that state through an injected context.

All runtime code lives in the **`TextEngine`** namespace and editor tooling in **`TextEngine.EditorTools`**, compiled into separate `TextEngine.Runtime` / `TextEngine.Editor` assemblies via asmdefs — so the engine imports cleanly into existing projects without class-name collisions.

An **EditMode test suite** (`TextEngine.Tests`, visible in `Window ▸ General ▸ Test Runner`) covers the Connect Four AI, the noun matcher, the content catalogs (including a duplicate-asset-name check, since saves are name-keyed), and save-file round-tripping. Save files carry a **format version** for forward migration, and the `GameController` validates its scene wiring on startup with actionable `[Text Engine]` errors instead of exceptions.

The engine works under **either input backend** — the legacy Input Manager or the Input System package — detected automatically at compile time, and the scene creator picks the matching UI input module.

Two small wrapper classes keep runtime state off the shared assets: **`EnemyInstance`** (blueprint + current health) and **`ItemInstance`** (blueprint reference; the hook for future per-copy state like durability or charges). Rooms, shops, the inventory, and equipment slots all hold instances — ScriptableObject assets are never mutated at runtime.

The parser resolves the typed verb through the `Action` catalog (keyword + synonyms), dispatches through a verb-handler table, and falls back to `CombatAction` then `CustomAction` assets — so the entire verb surface is data-driven.

---

## Project layout

```
Assets/
├── TextAdventureEngine/
│   ├── Runtime/          Engine code (TextEngine namespace, TextEngine.Runtime asmdef)
│   ├── Editor/           Custom inspectors + graph editors / validator / tool windows
│   ├── Tests/            EditMode test suite (TextEngine.Tests asmdef)
│   ├── Documentation/    Manual + store listing
│   └── Demo/
│       ├── Resources/    The demo game's content (see authoring table above)
│       ├── Scenes/       Demo scene + main menu
│       └── Prefabs/
└── TextMesh Pro/
```
