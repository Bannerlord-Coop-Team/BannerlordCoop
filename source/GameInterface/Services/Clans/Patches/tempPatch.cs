using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.GameDebug.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Clans.Patches
{
    [HarmonyPatch(typeof(Campaign), "OnAfterNewGameCreatedInternal")]
    public class tempPatch
    {
        static bool Prefix()
        {
            return false;
        }
    }
}
