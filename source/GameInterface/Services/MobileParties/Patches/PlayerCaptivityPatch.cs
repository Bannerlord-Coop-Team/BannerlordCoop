using Common.Messaging;
using Common.Util;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches
{
    [HarmonyPatch(typeof(PlayerCaptivity))]
    public class PlayerCaptivityPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("EndCaptivity")]
        public static bool EndCaptivity(ref PlayerCaptivity __instance)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;

            Hero prisoner = MobileParty.MainParty.LeaderHero;

            MessageBroker.Instance.Publish(prisoner, new PrisonerReleased(
                prisoner.StringId,
                3));

            return false;
        }
    }
}
