# Fool's Gambit — Mechanics

## Run start

Fool's Gambit replaces the selected character's normal starting relic set.

The relic stores the character's original Max HP, then sets Max HP and current HP to **1**.

## First combat: choose the deal

Before the first opening hand is drawn, choose one source for the Rare-card transformation:

| Mode | Rare-card source |
| --- | --- |
| Current Character | Current character's unlocked Rare cards |
| Colorless | Unlocked Rare Colorless cards |
| Random Character | One randomly selected playable character other than the current character |
| All Card Pools | Unlocked Rare cards across every available pool, including compatible modded pools |

Every transformable card currently in the deck is replaced card-for-card with a random Rare from the selected source. Duplicates are intentionally allowed. Engine-protected Eternal/untransformable deck cards are left alone rather than bypassing STS2's transformation safety rules.

Rare candidates and random-character candidates are sorted by stable model ID before any roll, then the Rare rolls use the player's seeded **Transformations** RNG stream. This prevents mod registration order from becoming multiplayer gameplay RNG. Replacement itself goes through STS2's native `CardCmd.Transform` pipeline, so deck history, add-to-deck modifiers and compatibility hooks still run. Because the combat draw pile has already been cloned before `BeforeHandDraw`, matching combat copies are transformed to the same new deck cards before the opening hand is drawn.

## HP regrowth

Fool's Gambit grows back 10% of the original Max HP after each of the first five combat victories.

The initial surviving 1 HP remains the base, so an 80-Max-HP character progresses approximately:

**1 → 9 → 17 → 25 → 33 → 41**

When Max HP grows, current HP grows by the same amount. Existing damage is preserved; this is not a full heal.

## Combat defence

At the beginning of every combat, gain **10 Plating**.

## Per-turn chaos

After the normal start-of-turn hand draw, Fool's Gambit chooses up to **2 distinct eligible cards** in hand.

Each chosen card independently rolls a cost of **0, 1, or 2** until the end of the turn or until that card is played.

X-cost and intrinsically non-energy-cost cards are excluded so the two rolls affect cards whose cost can actually change.

The two card identities use STS2's seeded **CombatCardSelection** RNG stream, while their 0–2 values use **CombatEnergyCosts**. The game's normal temporary card-cost system is used rather than a custom global cost override.
