# Text Adventure Engine — Manual

*Version 0.9.0 · Unity 6 (6000.x) · TextEngine namespace*

A data-driven engine for building parser-style text adventure games. Rooms, items, enemies, dialogue, quests, and shops are all authored as ScriptableObject assets — no code required — while the runtime handles parsing, combat, conversation, progression, and saving.

---

## 1. Installation & Quickstart

1. Import the package into a **Unity 6** project (TextMesh Pro essentials are required — Unity prompts if missing).
2. Run **`Tools ▸ Text Engine ▸ Create Game Scene`**. This builds a complete, wired scene: camera, canvas, scrolling output view, command input field, an EventSystem matching your project's input backend, a fully referenced `GameController`, and a `SoundManager` with its events connected.
3. Press **Play**. You're playing immediately (the demo's starting location is auto-assigned if present).
4. Build your world: create assets via **`Assets ▸ Create ▸ Text Adventure ▸ …`**, connect rooms in the **World Map Graph Editor**, write conversations in the **Dialogue Graph Editor**, and point the `GameController`'s starting locations at your own rooms.
5. Run **`Tools ▸ Text Engine ▸ Validate Content`** before every build.

### The one rule of content

**All content assets must live inside a folder named `Resources/`**, in the standard subfolders (`Locations/`, `Items/`, `Enemies/`, …). The engine loads its catalogs from there at startup, and the save system restores state by asset name. The **Content Root Folder** field on your Engine Settings asset tells the editor tools where to create new content — point it at your own game's `Resources/` folder to keep your content separate from the demo's.

> **Asset names are save keys.** Renaming a content asset orphans that piece of state in existing saves (skipped with a warning, never a crash). Duplicate asset names within a type collide — the Content Validator and the test suite both flag this.

---

## 2. Player commands

| Category | Commands |
|---|---|
| Movement & world | `go <dir>` (or `n`/`s`/`e`/`w`/`u`/`d`), `look [at <thing>]`, `take <item>`, `drop <item>`, `use <item> [on <target>]`, `push` / `pull` / `flush` / `activate <thing>` |
| Character | `inventory`, `equip` / `unequip <item>`, `equipment`, `status`, `char`, `skills [list]`, `learn <skill>`, `cast <skill> [on <target>]` |
| Combat & NPCs | `attack <enemy>`, `talk to <character>` |
| Economy & quests | `balance`, `buy <item>`, `sell <item>`, `quests` |
| System | `save`, `load`, `help` |

The parser ignores articles (`take the lantern`), matches partial names (`attack gob`), asks *"Which do you mean?"* on ambiguity, and recalls history with the arrow keys. Inside a shop: `buy` / `sell` / `leave`. With a trainer: `buy <skill>` / `leave`. In the minigame: a column number or `quit`.

---

## 3. Authoring reference

Create everything via `Assets ▸ Create ▸ Text Adventure`. Key fields per type:

### Location (`Resources/Locations/`)
The rooms of your world. **description** (shown on entry/look), **exits** (see below), default **items / interactables / characters / enemies** present on a new game, **backgroundMusic**, and shop settings (**isShop** + **shopInventory**). Use the World Map Graph Editor rather than editing exits by hand.

**Exits** hold: `direction` (North/East/South/West/Up/Down), `destination`, `isLocked` + `keyToUnlock` + `lockedDescription`, `isHidden` (revealed by dialogue/interactions/enemy defeat), and an optional `exitAction`:
- `StripAllItemsAndEquipment` — confiscates everything on passage.
- `InstaDeath` — ends the game with `deathMessage`.
- `BlockIfHoldingItem` — impassable while carrying `blockingItem` (message: `blockedMessage`).
- `ChangeLocationState` — reveals `itemsToReveal` in `targetLocation` (message: `stateChangeMessage`).

### Item (`Resources/Items/`)
**itemName** (what the player types), room/inventory **descriptions**, **itemType** (General, Weapon, Armor, Consumable, Currency, Key/Quest), equipment bonuses (**attackBonus**, **defenseBonus**, attribute bonuses — applied while equipped), consumable effects (**healthToRestore**, **drunkennessValue**, **effectToApplyOnUse**), **buyPrice/sellPrice**, and stacking (**isStackable**, **maxStackSize**). Currency items grant their `sellPrice` in coins when picked up.

### Enemy (`Resources/Enemies/`)
Stats (**maxHealth**, **baseAttack**, **damageVariance**, **evasionChance**), **xpReward**, **behavior** (an EnemyBehavior asset — without one the enemy uses a standard attack), **attacksOnSight** (ambushes on room entry), **lootDrops**, and **exitDirectionToReveal** on death.

### Enemy Behavior + Special Ability
A behavior is a prioritized list of **AI actions**, each gated by conditions (own health %, player health %). The first action whose conditions all pass fires its **Special Ability**: SelfHeal, StunPlayer (blocks the player's next attack *or* spell), DrainMana, CleanseDebuffs, ApplyDebuffToPlayer, ApplyBuffToSelf — with a magnitude and optional status-effect payload.

### Character (`Resources/Characters/`)
**characterName**, descriptions, **startingDialogue** (the conversation entry node), **becomesEnemy** (hostile form for the BecomeHostile dialogue action), **skillsToTeach** (makes them a trainer), and **connectFourDifficulty** (Easy / Medium / Hard) for the minigame.

### Dialogue Node (`Resources/Dialogue/`)
**dialogueText**, **playerResponses** (each response links to a next node; a response with no next node — or a node with no responses — ends the conversation), **requiredFlags** (all must be true, else the **failureNode** runs), **requiredItemForSuccess**, **flagsToSet**, and **successActions**:
GiveItem, TakeItem, RevealExit, StartMiniGame, BecomeHostile, StartQuest, UpdateQuest (marks `objectiveIndex` complete), CompleteQuest (pays rewards), OpenSkillShop, OpenShop.
Author trees visually in the **Dialogue Graph Editor**.

### Quest (`Resources/Quests/`)
**questName**, **questDescription**, **objectives** (list of strings; progress is tracked per-objective and saved), and rewards: **currencyReward**, **xpReward**, **itemRewards**.

### Skill (`Resources/Skills/`)
**Passive** skills permanently add attributes; **Active** skills cost **manaCost** and run an effect list (DealDamage, HealSelf, apply status effects) against a **targetType**. Gated by **requiredLevel** and bought with **skillPointCost** (via `learn`) or **currencyCost** (via trainers).

### Status Effect (`Resources/Status Effects/`)
DamageOverTime, HealOverTime, or attack/defense buffs & debuffs: **magnitude**, **durationInSeconds**, **tickInterval**, **chanceToApply**, and the messages shown on application/tick. Effects tick in real time and survive save/load with their remaining duration.

### Interactable (`Resources/Interactables/`)
A **noun** with a state machine. Each **interaction** matches a verb (or a used item) plus a **requiredState**, then fires effects: LogMessage, ChangeState, SetFlag, RevealExit, SpawnItemInRoom, ConsumeUsedItem. **allowedCustomActions** lists which custom verbs work on it.

### Action & Custom Action (`Resources/Actions/`, `Resources/Actions/Custom/`)
An **Action** defines a built-in verb keyword plus synonyms. A **Custom Action** is a brand-new verb — keyword, synonyms, failure message, and an effect list (give/take items, set flags, change target state, heal/damage, teleport, destroy the target) — no code required.

### Scenario, Flag Registry, Engine Settings
**Scenario**: a test setup (location, inventory, enemies) loadable at runtime via the Scenario Loader. **Flag Registry** (`GlobalFlagRegistry` in a Resources root): the master flag list feeding every flag dropdown. **Engine Settings**: feature toggles (leveling, attributes, drunkenness, status effects), all balance numbers, **verboseLogging**, and the editor **contentRootFolder**.

---

## 4. Editor tools

- **Create Game Scene** (`Tools ▸ Text Engine`) — one-click playable scene, described in Quickstart.
- **Validate Content** (`Tools ▸ Text Engine`) — audits every asset: broken exit destinations, locked doors without keys, dialogue actions missing items/quests, out-of-range quest objectives, flags missing from the registry, assets outside `Resources/`, shop misconfigurations, sell-price exploits, duplicate asset names. Severity-ranked, click-to-ping.
- **World Map Graph Editor** (`Window`) — every location as a node with six compass ports. Drag a direction into another room's *Entrance* to create an exit (the reciprocal exit is auto-created but independently deletable — one-way passages are easy). Re-dragging an exit retargets it while preserving its lock/hidden configuration. Full Undo support. Right-drag pans; `Ctrl+A` auto-arranges.
- **Dialogue Graph Editor** (`Window`) — every dialogue node as a graph node. Edit dialogue text inline, add responses with one click, drag response and failure ports between nodes to build the tree, and click a node to edit its flags/actions in the Inspector. Entry nodes are badged with the character who starts there. Auto-arrange lays out each conversation by depth. Full Undo support.
- **Flag Inspector** (`Window`) — live view of every world flag during Play Mode; toggle any flag to test branches. Merges the registry with runtime-set flags.
- **Scenario Loader** (`Window`) — jump the running game to a scenario instantly.
- **Custom inspectors** for locations, characters, enemies, and dialogue nodes, with "Create & Add" buttons that place new assets in your configured content root.

---

## 5. The save system

`save` / `load` serialize the complete game state as JSON to `Application.persistentDataPath/savegame.json`: player stats/name/drunkenness, inventory & equipment, learned skills, quests with per-objective progress, active status effects with remaining time, per-room items/enemies (with health)/characters/interactables, exit lock/visibility, shop stock, and world flags.

Save files carry a **format version** (`SaveData.CurrentVersion`); loading warns on mismatch and skips unrecognized data instead of crashing. Missing assets (renamed/deleted since the save) are skipped with warnings. A save pointing at a deleted location falls back to the main start location.

---

## 6. Extending the engine

**Namespaces**: runtime code is in `TextEngine`, editor tooling in `TextEngine.EditorTools`, compiled as `TextEngine.Runtime` / `TextEngine.Editor` asmdefs.

**UnityEvents** on the GameController (wire in the Inspector): `onItemTaken(Item)`, `onItemDropped(Item)`, `onLocationChanged(Location)`, `onEnemyDefeated(EnemyInstance)` — the built-in SoundManager uses these for pickup sounds and per-room music.

**Public API highlights**:
- `GameController.LoadScenario(TextEngineScenario)` — jump to a test scenario.
- `GameController.SkipTypewriter()` — finish streaming text immediately.
- `GameController.GetWorldFlag / SetWorldFlag / RuntimeFlagNames` — flag access for tools.
- `ConnectFourAI.ChooseColumn(board, difficulty)` and `ConnectFourBoard` — the minigame brain, reusable and pure.
- `NounMatcher.Find(source, nameOf, phrase, out ambiguousNames)` — the parser's fuzzy matcher, reusable for your own systems.

**Runtime instances**: rooms/shops/inventory hold `ItemInstance` / `EnemyInstance` wrappers around the ScriptableObject blueprints — **assets are never mutated at runtime**. Add per-copy state (durability, charges) to the instance classes.

**Input backends**: the engine compiles against the legacy Input Manager or the Input System package automatically; no configuration needed.

**Tests**: the `TextEngine.Tests` EditMode suite (`Window ▸ General ▸ Test Runner`) covers the minigame AI, the noun matcher, catalog integrity (including duplicate-name detection), and save round-tripping. Run it after content or engine changes.

---

## 7. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `[Text Engine] '…' is not assigned on the GameController` | A scene reference is missing. Use Create Game Scene, or assign the field named in the error. |
| Items/quests vanish from loaded saves | The asset isn't in a `Resources/` folder, or was renamed. Run Validate Content. |
| A verb does nothing | The keyword needs an Action asset (built-ins) or a Custom Action asset. Check `help` for the live verb list. |
| Shop/trainer won't open from dialogue | `isShop` is off on the location, or the character has no `skillsToTeach`. The console names the asset. |
| Flags don't trigger dialogue branches | Flag names must match the registry exactly — use the dropdowns, and inspect live values in the Flag Inspector. |
| Nothing happens on Play in a custom scene | Check the Console for `[Text Engine]` setup errors; the controller disables itself rather than throwing. |

*Support: see the Asset Store listing for contact details.*
