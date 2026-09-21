using BaseLib.Abstracts;
using BaseLib.Utils;
using FoolsGambitCode.Cards;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace FoolsGambitCode.Relics;

[Pool(typeof(SharedRelicPool))]
public sealed class FoolsGambit : CustomRelicModel
{
    private const int PlatingPerCombat = 10;
    private const int RandomizedCardsPerTurn = 2;
    private const int RecoveryCombats = 5;

    public override RelicRarity Rarity => RelicRarity.Starter;
    public override string PackedIconPath => $"{MainFile.ResPath}/images/relics/fools_gambit.svg";
    protected override string PackedIconOutlinePath => $"{MainFile.ResPath}/images/relics/fools_gambit_outline.svg";
    protected override string BigIconPath => $"{MainFile.ResPath}/images/relics/fools_gambit.svg";

    public override List<(string, string)>? Localization =>
        new RelicLoc(
            "Fool's Gambit",
            "Set your Max HP to 1. Regrow 10% of your original Max HP after each of your first 5 combat victories. Start each combat with 10 Plating. Before your first hand, choose a card pool and transform your deck into random Rare cards. Each turn, 2 random cards in your hand cost 0-2 this turn.",
            "The joke is statistically on someone.",
            ("poolPrompt", "Choose what gets dealt.")
        );

    [SavedProperty]
    public int OriginalMaxHp { get; set; }

    [SavedProperty]
    public int RecoveryVictories { get; set; }

    [SavedProperty]
    public bool DeckTransformed { get; set; }

    [SavedProperty]
    public int SelectedPoolMode { get; set; } = -1;

    /// <summary>
    /// Starting relics are inserted before the player is attached to a live run, so
    /// their normal AfterObtained hook is not used. The starting-relic patch calls
    /// this directly while the player's original HP is still available.
    /// </summary>
    public void InitializeForNewRun()
    {
        if (OriginalMaxHp > 0)
            return;

        OriginalMaxHp = Owner.Creature.MaxHp;
        Owner.Creature.SetMaxHpInternal(1m);
        Owner.Creature.SetCurrentHpInternal(1m);
    }

    public override async Task AfterObtained()
    {
        // Defensive fallback for console/debug acquisition. Fool's Gambit is normally
        // a starter and reaches InitializeForNewRun() instead.
        if (OriginalMaxHp <= 0)
            OriginalMaxHp = Owner.Creature.MaxHp;

        await CreatureCmd.SetMaxHp(Owner.Creature, 1m);
        await CreatureCmd.SetCurrentHp(Owner.Creature, 1m);
    }

    public override async Task BeforeHandDraw(
        Player player,
        PlayerChoiceContext choiceContext,
        ICombatState combatState)
    {
        if (player != Owner || Owner.PlayerCombatState is not { TurnNumber: 1 })
            return;

        await PowerCmd.Apply<PlatingPower>(
            choiceContext,
            Owner.Creature,
            PlatingPerCombat,
            Owner.Creature,
            null);

        if (!DeckTransformed)
            await ChoosePoolAndTransform(choiceContext, combatState);
    }

    public override Task AfterAutoPrePlayPhaseEntered(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner)
            return Task.CompletedTask;

        // AutoPrePlay begins only after turn setup and the normal hand draw are complete,
        // so the two rolls always target cards that are actually in the player's hand.
        RollTurnCosts();
        return Task.CompletedTask;
    }

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (RecoveryVictories >= RecoveryCombats || OriginalMaxHp <= 0)
            return;

        // Compute recovery as a delta, not an absolute Max-HP target. This
        // matters if another relic/event has already increased Max HP: those
        // gains should not consume Fool's Gambit's promised regrowth.
        var recoveredBefore = (int)decimal.Ceiling(
            OriginalMaxHp * 0.10m * RecoveryVictories);

        RecoveryVictories++;

        var recoveredAfter = (int)decimal.Ceiling(
            OriginalMaxHp * 0.10m * RecoveryVictories);

        var hpGained = recoveredAfter - recoveredBefore;
        if (hpGained > 0)
        {
            var newMaxHp = Owner.Creature.MaxHp + hpGained;
            var newCurrentHp = Math.Min(
                newMaxHp,
                Owner.Creature.CurrentHp + hpGained);

            await CreatureCmd.SetMaxHp(Owner.Creature, newMaxHp);

            // "Regrow" the gained capacity as real HP too, while preserving
            // existing damage. This is deliberately not a full heal.
            await CreatureCmd.SetCurrentHp(Owner.Creature, newCurrentHp);
        }

        Flash();
    }

    public override Task AfterCombatEnd(CombatRoom room) =>
        Task.CompletedTask;

    private async Task ChoosePoolAndTransform(
        PlayerChoiceContext choiceContext,
        ICombatState combatState)
    {
        var candidates = new List<CardModel>
        {
            combatState.CreateCard(ModelDb.Card<CurrentCharacterChoice>(), Owner),
            combatState.CreateCard(ModelDb.Card<ColorlessChoice>(), Owner),
            combatState.CreateCard(ModelDb.Card<RandomCharacterChoice>(), Owner),
            combatState.CreateCard(ModelDb.Card<AllPoolsChoice>(), Owner)
        };

        var prompt = new LocString("relics", $"{Id.Entry}.poolPrompt");
        var selected = (await CardSelectCmd.FromSimpleGrid(
                choiceContext,
                candidates,
                Owner,
                new CardSelectorPrefs(prompt, 1)))
            .OfType<IGambitChoice>()
            .FirstOrDefault();

        var selectedMode = selected?.Mode;

        // These are UI-only token cards. Mark them removed once the choice has
        // been captured so they do not remain live in the combat card scope.
        foreach (var candidate in candidates)
            candidate.RemoveFromState();

        if (selectedMode == null)
            return;

        SelectedPoolMode = (int)selectedMode.Value;

        var rarePool = GetRarePool(selectedMode.Value);
        if (rarePool.Count == 0)
        {
            // A custom character can legally expose no Rare cards. Falling
            // back to All Pools keeps the run playable instead of reprompting
            // forever at the next combat.
            MainFile.Logger.Warn(
                $"Fool's Gambit found no Rare cards for mode {selectedMode.Value}; " +
                "falling back to All Card Pools.");

            SelectedPoolMode = (int)GambitPoolMode.AllPools;
            rarePool = GetRarePool(GambitPoolMode.AllPools);
        }

        if (rarePool.Count == 0)
        {
            MainFile.Logger.Error(
                "Fool's Gambit found no Rare cards in any available pool; " +
                "leaving the deck unchanged.");
            DeckTransformed = true;
            return;
        }

        await TransformStartingDeck(rarePool, combatState);
        DeckTransformed = true;
        Flash();
    }

    private List<CardModel> GetRarePool(GambitPoolMode mode)
    {
        IEnumerable<CardModel> cards = mode switch
        {
            GambitPoolMode.CurrentCharacter =>
                GetUnlockedRareCards(Owner.Character.CardPool),

            GambitPoolMode.Colorless =>
                GetUnlockedRareCards(ModelDb.CardPool<ColorlessCardPool>()),

            GambitPoolMode.RandomCharacter =>
                GetRandomCharacterRareCards(),

            GambitPoolMode.AllPools =>
                ModelDb.AllCardPools.SelectMany(GetUnlockedRareCards),

            _ => Enumerable.Empty<CardModel>()
        };

        return cards
            .Where(card => card.Rarity == CardRarity.Rare)
            .DistinctBy(card => card.Id)
            // Custom content registration order should not become gameplay RNG.
            // Sort by stable model ID so multiplayer peers roll against the
            // exact same list even when several mods contribute card pools.
            .OrderBy(card => card.Id.Entry, StringComparer.Ordinal)
            .ToList();
    }

    private IEnumerable<CardModel> GetUnlockedRareCards(CardPoolModel pool) =>
        pool.GetUnlockedCards(
                Owner.UnlockState,
                Owner.RunState.CardMultiplayerConstraint)
            .Where(card => card.Rarity == CardRarity.Rare);

    private IEnumerable<CardModel> GetRandomCharacterRareCards()
    {
        var options = ModelDb.AllCharacters
            .Where(character =>
                character.IsPlayable &&
                character.Id != Owner.Character.Id)
            .Select(character => new
            {
                Character = character,
                Rares = GetUnlockedRareCards(character.CardPool).ToList()
            })
            .Where(x => x.Rares.Count > 0)
            .OrderBy(x => x.Character.Id.Entry, StringComparer.Ordinal)
            .ToList();

        if (options.Count == 0)
            return GetUnlockedRareCards(Owner.Character.CardPool);

        var chosen = Owner.PlayerRng.Transformations.NextItem(options);
        MainFile.Logger.Info($"Fool's Gambit random character pool: {chosen.Character.Id}");
        return chosen.Rares;
    }

    private async Task TransformStartingDeck(
        IReadOnlyList<CardModel> rarePool,
        ICombatState combatState)
    {
        var deck = PileType.Deck.GetPile(Owner);
        var originals = deck.Cards.ToList();
        if (originals.Count == 0)
            return;

        // Respect STS2's untransformable-card contract rather than forcibly
        // ripping engine-protected cards out of the deck. Normal starting cards,
        // including Plaguebringer's, are expected to be transformable.
        var transformable = originals
            .Where(card => card.IsTransformable)
            .ToList();

        var skipped = originals.Count - transformable.Count;
        if (skipped > 0)
        {
            MainFile.Logger.Warn(
                $"Fool's Gambit skipped {skipped} untransformable deck card(s).");
        }

        var planned = new List<(CardModel Original, CardTransformation Transform)>(
            transformable.Count);

        foreach (var original in transformable)
        {
            var canonicalRare = Owner.PlayerRng.Transformations.NextItem(rarePool);
            var replacement = Owner.RunState.CreateCard(canonicalRare, Owner);

            planned.Add((
                original,
                new CardTransformation(original, replacement)));
        }

        // Use the game's native transformation pipeline for the permanent deck.
        // This preserves deck history, add-to-deck modifiers, state cleanup and
        // compatibility hooks used by other mods.
        var deckResults = (await CardCmd.Transform(
                planned.Select(pair => pair.Transform),
                rng: null,
                style: CardPreviewStyle.None))
            .ToList();

        var replacementsByOriginal =
            new Dictionary<CardModel, CardModel>(ReferenceEqualityComparer.Instance);

        for (var i = 0; i < Math.Min(planned.Count, deckResults.Count); i++)
        {
            var result = deckResults[i];
            if (!result.success || result.cardAdded == null)
                continue;

            replacementsByOriginal[planned[i].Original] = result.cardAdded;
        }

        // Combat has already cloned the deck by the time BeforeHandDraw runs.
        // Transform the matching draw-pile copies as well so turn 1 actually
        // draws the new Rare deck instead of the old starter cards.
        var drawPile = PileType.Draw.GetPile(Owner);
        var combatTransforms = new List<CardTransformation>();

        foreach (var combatCard in drawPile.Cards.ToList())
        {
            var oldDeckVersion = combatCard.DeckVersion;
            if (oldDeckVersion == null ||
                !replacementsByOriginal.TryGetValue(oldDeckVersion, out var newDeckVersion))
            {
                continue;
            }

            if (!combatCard.IsTransformable)
            {
                MainFile.Logger.Warn(
                    $"Fool's Gambit could not transform combat copy {combatCard.Id}; " +
                    "the card is marked untransformable.");
                continue;
            }

            var combatReplacement = combatState.CloneCard(newDeckVersion);
            combatReplacement.DeckVersion = newDeckVersion;

            combatTransforms.Add(
                new CardTransformation(combatCard, combatReplacement));
        }

        if (combatTransforms.Count > 0)
        {
            await CardCmd.Transform(
                combatTransforms,
                rng: null,
                style: CardPreviewStyle.None);
        }

        MainFile.Logger.Info(
            $"Fool's Gambit transformed {deckResults.Count(result => result.success)}/" +
            $"{originals.Count} deck cards using {(GambitPoolMode)SelectedPoolMode}; " +
            $"updated {combatTransforms.Count} combat copy/copies.");
    }

    private void RollTurnCosts()
    {
        var eligible = PileType.Hand.GetPile(Owner).Cards
            .Where(card =>
                !card.EnergyCost.CostsX &&
                card.EnergyCost.Canonical >= 0)
            .ToList();

        if (eligible.Count == 0)
            return;

        // Keep "which cards?" and "what costs?" on STS2's dedicated seeded
        // combat streams. This keeps save/load deterministic without consuming
        // the persistent deck-transformation RNG stream.
        var selectionRng = Owner.RunState.Rng.CombatCardSelection;
        var costRng = Owner.RunState.Rng.CombatEnergyCosts;
        var count = Math.Min(RandomizedCardsPerTurn, eligible.Count);

        for (var i = 0; i < count; i++)
        {
            var index = selectionRng.NextInt(eligible.Count);
            var card = eligible[index];
            eligible.RemoveAt(index);

            var rolledCost = costRng.NextInt(0, 3);
            card.EnergyCost.SetThisTurnOrUntilPlayed(rolledCost, reduceOnly: false);

            // Reuse the game's Snecko-style feedback so the player can clearly
            // see which two cards were hit by the Gambit.
            NCard.FindOnTable(card, null)?.PlayRandomizeCostAnim();

            MainFile.Logger.Debug(
                $"Fool's Gambit turn cost: card={card.Id}, cost={rolledCost}");
        }

        Flash();
    }
}
