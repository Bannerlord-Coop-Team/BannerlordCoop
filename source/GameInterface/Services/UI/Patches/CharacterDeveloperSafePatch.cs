using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;

namespace GameInterface.Services.UI.Patches;

[HarmonyPatch]
internal class CharacterDeveloperSafePatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<CharacterDeveloperSafePatch>();

    [HarmonyPatch(typeof(CharacterDeveloperState), nameof(CharacterDeveloperState.OnInitialize))]
    [HarmonyFinalizer]
    static System.Exception OnInitialize_Finalizer(System.Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error("UI CharacterDeveloperState.OnInitialize exception: {Exception}", __exception);
            InformationManager.DisplayMessage(new InformationMessage("[UI] Erreur ouverture écran personnage (ignorée)"));
            return null;
        }
        return null;
    }

    [HarmonyPatch(typeof(CharacterDeveloperState), nameof(CharacterDeveloperState.OnActivate))]
    [HarmonyFinalizer]
    static System.Exception OnActivate_Finalizer(System.Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error("UI CharacterDeveloperState.OnActivate exception: {Exception}", __exception);
            InformationManager.DisplayMessage(new InformationMessage("[UI] Erreur activation écran personnage (ignorée)"));
            return null;
        }
        return null;
    }
}
