using System.Reflection;
using FoolsGambitCode.Cards;
using FoolsGambitCode.Relics;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace FoolsGambitCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "FOOLSGAMBIT";
    public const string ResPath = "res://FoolsGambit";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    private static Harmony? _harmony;
    private static bool _contentRegistered;

    public static void Initialize()
    {
        RegisterContentModels();

        _harmony ??= new Harmony(ModId);
        _harmony.PatchAll(Assembly.GetExecutingAssembly());

        Logger.Info("Banter's - Fool's Gambit loaded.");
    }

    private static void RegisterContentModels()
    {
        if (_contentRegistered)
            return;

        _contentRegistered = true;

        Type[] modelTypes =
        [
            typeof(FoolsGambit),
            typeof(CurrentCharacterChoice),
            typeof(ColorlessChoice),
            typeof(RandomCharacterChoice),
            typeof(AllPoolsChoice)
        ];

        foreach (var type in modelTypes)
            Activator.CreateInstance(type);
    }
}
