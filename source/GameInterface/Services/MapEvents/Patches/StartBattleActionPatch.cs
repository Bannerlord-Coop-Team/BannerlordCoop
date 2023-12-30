using Common.Messaging;
using Common.Util;
using Common;
using GameInterface.Services.GameDebug.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using GameInterface.Services.MapEvents.Messages;
using System.Diagnostics;
using Common.Logging;
using GameInterface.Services.MobileParties.Handlers;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using System.Collections.Concurrent;

namespace GameInterface.Services.MapEvents.Patches
{
    [HarmonyPatch(typeof(MobileParty))]
    public class StartBattleActionPatch
    {
        [HarmonyPatch("TaleWorlds.CampaignSystem.Map.IMapEntity.OnPartyInteraction")]
        static bool Prefix(MobileParty __instance, MobileParty engagingParty)
        {
            if (ModInformation.IsClient) return false;

            MobileParty mobileParty = __instance;

            if (mobileParty.AttachedTo != null && engagingParty != mobileParty.AttachedTo)
            {
                mobileParty = mobileParty.AttachedTo;
            }

            MessageBroker.Instance.Publish(engagingParty, new BattleStarted(
                engagingParty.StringId,
                mobileParty.StringId));

            return false;
        }
    }
}
