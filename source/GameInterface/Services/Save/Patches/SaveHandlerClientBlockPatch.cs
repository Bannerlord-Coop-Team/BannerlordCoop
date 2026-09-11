using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Save.Patches
{
    [HarmonyPatch(typeof(SaveHandler), "SetSaveArgs")]
    internal class SaveHandlerClientBlockPatch
    {
        internal static bool Prefix() => ModInformation.IsServer
#if DEBUG
            && !ModInformation.IsNavalLab
#endif
            ;
    }
}
