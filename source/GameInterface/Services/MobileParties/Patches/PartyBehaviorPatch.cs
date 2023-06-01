using TaleWorlds.CampaignSystem.Party;
using HarmonyLib;
using TaleWorlds.Library;
using GameInterface.Extentions;
using Common.Messaging;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem.Settlements;
using ProtoBuf;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages;
using System.Reflection;
using GameInterface.Utils;

namespace GameInterface.Services.MobileParties.Patches
{
    [HarmonyPatch(typeof(MobilePartyAi))]
    static class PartyBehaviorPatch
    {
        public static void SetAiBehavior(
            MobilePartyAi partyAi, AiBehavior newBehavior, IMapEntity targetMapEntity, Vec2 targetPoint)
        {
            ReflectionUtils.InvokePrivateMethod(typeof(MobilePartyAi), "SetShortTermBehavior", partyAi, new object[] { newBehavior, targetMapEntity });
            ReflectionUtils.SetPrivateField(typeof(MobilePartyAi), "BehaviorTarget", partyAi, targetPoint);
            ReflectionUtils.InvokePrivateMethod(typeof(MobilePartyAi), "UpdateBehavior", partyAi);
        }

        [HarmonyPrefix]
        [HarmonyPatch("SetAiBehavior")]
        private static bool SetAiBehaviorPrefix(
            ref MobilePartyAi __instance, 
            ref AiBehavior newAiBehavior, 
            ref PartyBase targetPartyFigure, 
            ref Vec2 bestTargetPoint)
        {
            MobileParty party = __instance.GetMobileParty();

            bool hasTargetEntity = false;
            string targetEntityId = string.Empty;

            if (targetPartyFigure != null)
            {
                hasTargetEntity = true;
                targetEntityId = targetPartyFigure.IsSettlement
                    ? targetPartyFigure.Settlement.StringId
                    : targetPartyFigure.MobileParty.StringId;
            }

            var data = new AiBehaviorUpdateData(party.StringId, newAiBehavior, hasTargetEntity, targetEntityId, bestTargetPoint);
            MessageBroker.Instance.Publish(__instance, new PartyAiBehaviorChanged(party, data));

            return false;
        }
    }
}
