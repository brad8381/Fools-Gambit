using FoolsGambitCode.Relics;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;

namespace FoolsGambitCode.Patches;

/// <summary>
/// Replaces the character's normal starting relic set before it is populated.
/// Doing this as a prefix avoids briefly adding/removing starter relics and avoids
/// firing any removal-side effects from custom-character starter relics.
/// </summary>
[HarmonyPatch(typeof(Player), "PopulateStartingRelics")]
internal static class StartingRelicPatch
{
    [HarmonyPrefix]
    private static bool ReplaceStartingRelics(Player __instance)
    {
        var gambit = ModelDb.Relic<FoolsGambit>().ToMutable();
        gambit.FloorAddedToDeck = 1;

        __instance.AddRelicInternal(gambit, index: -1, silent: false);
        gambit.InitializeForNewRun();

        SaveManager.Instance?.MarkRelicAsSeen(gambit);

        MainFile.Logger.Info(
            $"Replaced starting relic set with Fool's Gambit for {__instance.Character.Id}; " +
            $"originalMaxHp={gambit.OriginalMaxHp}.");

        // Skip Player.PopulateStartingRelics entirely so the character's normal
        // starter relic(s) are never inserted.
        return false;
    }
}
