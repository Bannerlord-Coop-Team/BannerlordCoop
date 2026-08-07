using Common;
using GameInterface.Configuration;
using HarmonyLib;
using TaleWorlds.Engine;

namespace GameInterface.Services.CampaignService.Patches;

[HarmonyPatch(typeof(NativeConfig))]
internal class BlockClientCheatsPatch
{
    [HarmonyPatch(nameof(NativeConfig.CheatMode), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool CheatModeGetterPrefix(ref bool __result)
    {
        if (ModInformation.IsClient && !ModConfigProvider.ModOptions.ClientsCanUseCheats)
        {
            __result = false;
            return false;
        }

        return true;
    }
}
