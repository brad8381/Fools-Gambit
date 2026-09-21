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
using MegaCrit.Sts2.Core.Nodes.Rooms;
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
            "Set your Max HP to 1. Over your first 5 combat victories, regrow 50% of your original Max HP. Start each combat with 10 Plating. Your starting deck becomes random Rare cards. At the start of each turn, 2 random cards in your hand cost 0-2 this turn.",
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

        RecoveryVictories++;

        // The surviving 1 HP remains the base, then 10% of the original Max HP
        // grows back per victory. Example: 80 -> 1, 9, 17, 25, 33, 41.
        var desiredMaxHp =
            1 + (int)decimal.Ceiling(OriginalMaxHp * 0.10m * RecoveryVictories);

        if (Owner.Creature.MaxHp < desiredMaxHp)
        {
            var hpGained = desiredMaxHp - Owner.Creature.MaxHp;
            var oldCurrentHp = Owner.Creature.CurrentHp;

            await CreatureCmd.SetMaxHp(Owner.Creature, desiredMaxHp);

            // "Regrow" the gained capacity as real HP too, while preserving any
            // damage already taken. This is not a full heal.
            await CreatureCmd.SetCurrentHp(
                Owner.Creature,
                Math.Min(desiredMaxHp, oldCurrentHp + hpGained));
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

        if (selected == null)
            return;

        SelectedPoolMode = (int)selected.Mode;

        var rarePool = GetRarePool(selected.Mode);
        if (rarePool.Count == 0)
        {
            MainFile.Logger.Error($"Fool's Gambit found no Rare cards for mode {selected.Mode}.");
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
        var oldDeck = PileType.Deck.GetPile(Owner).Cards.ToList();
        if (oldDeck.Count == 0)
            return;

        var replacements = new List<(CardModel Old, CardModel New)>(oldDeck.Count);

        foreach (var oldCard in oldDeck)
        {
            var canonicalRare = Owner.PlayerRng.Transformations.NextItem(rarePool);
            var newCard = Owner.RunState.CreateCard(canonicalRare, Owner);
            newCard.FloorAddedToDeck = oldCard.FloorAddedToDeck ?? 1;
            replacements.Add((oldCard, newCard));
        }

        await CardPileCmd.RemoveFromDeck(oldDeck, showPreview: false);
        await CardPileCmd.Add(
            replacements.Select(pair => pair.New),
            PileType.Deck,
            CardPilePosition.Bottom,
            clonedBy: this,
            skipVisuals: true);

        var drawPile = PileType.Draw.GetPile(Owner);
        var oldCombatCards = drawPile.Cards.ToList();

        await CardPileCmd.RemoveFromCombat(oldCombatCards, skipVisuals: true);

        foreach (var oldCombatCard in oldCombatCards)
        {
            var pair = replacements.FirstOrDefault(x =>
                ReferenceEquals(x.Old, oldCombatCard.DeckVersion));

            if (pair.New == null)
                continue;

            var newCombatCard = combatState.CloneCard(pair.New);
            newCombatCard.DeckVersion = pair.New;
            drawPile.AddInternal(newCombatCard, -1, silent: true);
        }

        MainFile.Logger.Info(
            $"Fool's Gambit transformed {replacements.Count} starting cards using {(GambitPoolMode)SelectedPoolMode}.");
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

        // STS2 has a dedicated seeded stream for combat cost rolls. Using it
        // makes the result deterministic across save/load and avoids consuming
        // the deck-transformation RNG stream.
        var rng = Owner.RunState.Rng.CombatEnergyCosts;
        var count = Math.Min(RandomizedCardsPerTurn, eligible.Count);

        for (var i = 0; i < count; i++)
        {
            var index = rng.NextInt(eligible.Count);
            var card = eligible[index];
            eligible.RemoveAt(index);

            var rolledCost = rng.NextInt(0, 3);
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
