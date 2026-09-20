using FoolsGambitCode.Relics;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace FoolsGambitCode.Patches;

[HarmonyPatch(typeof(Player), "PopulateStartingRelics")]
internal static class StartingRelicPatch
{
    [HarmonyPostfix]
    private static void ReplaceStartingRelics(Player __instance)
    {
        foreach (var relic in __instance.Relics.ToList())
            __instance.RemoveRelicInternal(relic, silent: true);

        var gambit = ModelDb.Relic<FoolsGambit>().ToMutable();
        gambit.FloorAddedToDeck = 1;
        __instance.AddRelicInternal(gambit, index: -1, silent: true);

        MainFile.Logger.Info(
            $"Replaced starting relic(s) with Fool's Gambit for {__instance.Character.Id}.");
    }
}
