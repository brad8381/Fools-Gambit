using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace FoolsGambitCode.Cards;

public enum GambitPoolMode
{
    CurrentCharacter = 0,
    Colorless = 1,
    RandomCharacter = 2,
    AllPools = 3
}

public interface IGambitChoice
{
    GambitPoolMode Mode { get; }
}

public abstract class GambitChoiceCard : CustomCardModel, IGambitChoice
{
    protected GambitChoiceCard(GambitPoolMode mode)
        : base(0, CardType.Skill, CardRarity.Token, TargetType.None, showInCardLibrary: false)
    {
        Mode = mode;
    }

    public GambitPoolMode Mode { get; }

    public override string? CustomPortraitPath =>
        $"{MainFile.ResPath}/images/cards/fools_choice.svg";
}

public sealed class CurrentCharacterChoice : GambitChoiceCard
{
    public CurrentCharacterChoice() : base(GambitPoolMode.CurrentCharacter) { }

    public override List<(string, string)>? Localization =>
        new CardLoc(
            "Current Character",
            "Transform every card in your starting deck into a random Rare card from your current character. Duplicates allowed."
        );
}

public sealed class ColorlessChoice : GambitChoiceCard
{
    public ColorlessChoice() : base(GambitPoolMode.Colorless) { }

    public override List<(string, string)>? Localization =>
        new CardLoc(
            "Colorless",
            "Transform every card in your starting deck into a random Rare Colorless card. Duplicates allowed."
        );
}

public sealed class RandomCharacterChoice : GambitChoiceCard
{
    public RandomCharacterChoice() : base(GambitPoolMode.RandomCharacter) { }

    public override List<(string, string)>? Localization =>
        new CardLoc(
            "Random Character",
            "Roll one other playable character, then transform every card in your starting deck into a random Rare card from that character. Duplicates allowed."
        );
}

public sealed class AllPoolsChoice : GambitChoiceCard
{
    public AllPoolsChoice() : base(GambitPoolMode.AllPools) { }

    public override List<(string, string)>? Localization =>
        new CardLoc(
            "All Card Pools",
            "Transform every card in your starting deck into a random Rare card from any available card pool. Maximum chaos. Duplicates allowed."
        );
}
