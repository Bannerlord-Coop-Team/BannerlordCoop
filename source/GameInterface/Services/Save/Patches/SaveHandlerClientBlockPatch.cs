using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Save.Patches
{
    [HarmonyPatch(typeof(SaveHandler), "SetSaveArgs")]
    internal class SaveHandlerClientBlockPatch
    {
        static bool Prefix() => !ModInformation.IsClient;
    }
}
