# Asset Store Listing Copy

*Ready-to-paste text for the Unity Asset Store publisher portal. Screenshots/key images still to be produced — the World Map Graph Editor and Dialogue Graph Editor are the money shots; include a WebGL demo link.*

---

## Title

**Text Adventure Engine — Parser-Based Interactive Fiction Toolkit**

## Summary (one-liner)

Build classic parser-driven text adventures entirely from the Inspector: visual world & dialogue graph editors, smart parser, combat, quests, shops, and a complete save system — no code required.

## Description

**Type `go north`. Ship a world.**

Text Adventure Engine is a complete, data-driven toolkit for parser-style interactive fiction in Unity. Every room, item, enemy, conversation, quest, and shop is a ScriptableObject — designers build entire games without writing a line of code, while programmers get a clean, namespaced, fully documented API to extend.

**A parser players won't fight.** Articles are ignored, partial names match (`attack gob`), ambiguity gets a polite *"Which do you mean?"*, direction shortcuts and command history are built in — and every verb is data-driven, so adding a new one is an asset, not a script.

**Visual editors that do the heavy lifting:**
- **World Map Graph Editor** — drag compass ports between room nodes to build your map; reciprocal exits auto-create, one-way passages are one delete away, and exit re-drags preserve your lock/hidden configuration. Full Undo.
- **Dialogue Graph Editor** — write branching conversations on a canvas: inline text editing, one-click responses, drag-to-link branches and failure paths, per-character entry badges.
- **Content Validator** — one click audits your entire game for broken references, missing keys, out-of-range quest objectives, and save-breaking name collisions.
- **One-click scene setup**, a live **Flag Inspector**, and a **Scenario Loader** for instant playtesting.

**Deep game systems, all optional:** turn-based combat with equipment bonuses and enemy AI behaviors · status effects · leveling with primary attributes · active & passive skills with trainers · branching dialogue gated by flags and items · multi-objective quests · shops and currency · even a built-in Connect Four minigame with a three-level AI you can assign per NPC (Hard plays nearly perfectly).

**A save system you can trust.** Complete world state — every room, enemy health bar, shop shelf, quest objective, and status-effect timer — serialized to versioned JSON, with resilient loading that warns instead of crashes.

**Built to professional standards:** namespaced code under asmdefs, zero compile warnings, an automated test suite covering the parser, AI, catalogs, and save round-tripping, and a full manual.

Works with both the legacy Input Manager and the new Input System. Demo game included.

## Technical details

- Unity 6 (6000.x); uGUI + TextMesh Pro
- Runtime: `TextEngine` namespace, `TextEngine.Runtime` asmdef; editor tooling separated in `TextEngine.Editor`
- All content as ScriptableObjects loaded from Resources; name-keyed versioned JSON saves
- EditMode test suite included; zero-warning compile
- Source code included, documented throughout

## Keywords

text adventure, interactive fiction, parser, IF, dialogue system, quest system, node editor, narrative, retro, terminal, MUD, Zork
