# Fool's Gambit — Test Plan

## Build smoke test

1. Restore/build against the installed STS2 Main DLL and BaseLib.
2. Publish so the PCK containing the relic and choice art is exported.
3. Start the game with BaseLib + Fool's Gambit only.
4. Confirm the mod loads without model-registration or localization errors.

## New run

For each vanilla playable character:

1. Start a run.
2. Confirm the normal starter relic is absent.
3. Confirm Fool's Gambit is present.
4. Confirm Max HP/current HP are both 1 before entering the first combat.
5. Enter combat and confirm 10 Plating.
6. Confirm the four pool choices appear before the opening hand.
7. Select each pool mode on separate runs.
8. Confirm deck size is unchanged and every transformed card is Rare.
9. Confirm duplicate Rares are allowed.
10. Confirm the opening draw comes from the transformed deck.

## Cost randomization

1. Start a player turn with at least two normal-cost cards.
2. Confirm exactly two distinct eligible cards receive a 0–2 roll.
3. Confirm the visible cost updates.
4. Confirm Snecko-style cost-change feedback plays.
5. Play one modified card; confirm the temporary override does not incorrectly persist.
6. End the turn; confirm remaining temporary overrides clear.
7. Confirm X-cost and unplayable cards do not consume one of the two eligible selections.

## HP recovery

For a character starting at 80 Max HP, verify victory progression:

**1 → 9 → 17 → 25 → 33 → 41**

Take damage before a victory and verify the Max-HP gain also increases current HP by only the gained capacity rather than performing a full heal.

Confirm there is no additional recovery after victory 5.

## Save/load

1. Save after pool selection; reload and confirm the deck is not transformed a second time.
2. Save after 1–4 victories; reload and confirm OriginalMaxHp and RecoveryVictories persist.
3. Save during a run with modified Max HP and confirm current/max HP restore correctly.

## Mod compatibility

1. Run with Plaguebringer enabled.
2. Confirm Fool's Gambit replaces Plaguebringer's starter relic.
3. Confirm Current Character uses Plaguebringer Rare cards.
4. Confirm All Card Pools can include Plaguebringer Rares.
5. Confirm disabling Fool's Gambit restores the character's normal starter behavior.

## Multiplayer

1. Start a multiplayer run with all clients using the mod.
2. Confirm each player receives exactly one Fool's Gambit.
3. Confirm each player's pool choice affects only their deck.
4. Confirm deck transformation is not duplicated by network replay.
5. Confirm seeded cost rolls remain synchronized.
