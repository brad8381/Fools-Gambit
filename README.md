# Banter's - Fool's Gambit

A Slay the Spire 2 chaos starting-relic mod.

## Fool's Gambit

Every character starts with **Fool's Gambit** instead of their normal starting relic.

Before the first opening hand, choose a Rare-card source:

- **Current Character** — unlocked Rare cards from your current character.
- **Colorless** — unlocked Rare Colorless cards.
- **Random Character** — Rare cards from one randomly selected playable character other than your current character.
- **All Card Pools** — unlocked Rare cards from every available card pool, including compatible modded pools.

Your deck is then transformed card-for-card into random Rares from that source. Duplicates are deliberately allowed.

Additional effects:

- Store your original Max HP, then start at **1/1 HP**.
- After each of the first five combat victories, regrow **10% of your original Max HP**. The gained capacity also regrows as current HP without fully healing existing damage.
- Start each combat with **10 Plating**.
- After the normal start-of-turn draw, **2 distinct eligible cards in hand** independently have their Energy cost randomized to **0–2** for that turn or until played.
- X-cost and intrinsically non-energy-cost cards are excluded from the two cost rolls.

The intent is deliberately high variance: the transformed deck can be awful, strong, or completely broken.

## Development status

The first implementation is on the `feature/fools-gambit-relic` branch. It has been statically checked against the current STS2/Main API shape and BaseLib patterns, but still needs a real compile/game smoke test against an installed copy of STS2.

See:

- `docs/MECHANICS.md` for exact behaviour.
- `docs/TEST_PLAN.md` for the first in-game validation pass.

## Local build/install

Copy `local.props.example` to `local.props` if your STS2 or MegaDot paths are not auto-detected, then run:

```powershell
.\Build-Install.ps1
```

This restores, builds/installs the DLL, then publishes/exports the PCK.

Requires [BaseLib](https://github.com/Alchyr/BaseLib-StS2).
