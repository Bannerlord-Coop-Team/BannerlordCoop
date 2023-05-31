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
        public static void SetAiBehavior(AiBehaviorUpdateData data)
        {
            var objectManager = ServiceLocator.Resolve<IObjectManager>();

            IMapEntity mapEntity = null;

            if (!objectManager.TryGetObject(data.PartyId, out MobileParty party) || 
                (data.HasTarget && !objectManager.TryGetObject(data.TargetId, out mapEntity)))
            {
                return;
            }

            ReflectionUtils.InvokePrivateMethod(typeof(MobilePartyAi), "SetShortTermBehavior", party.Ai, new object[] { data.Behavior, mapEntity });
            ReflectionUtils.SetPrivateField(typeof(MobilePartyAi), "BehaviorTarget", party.Ai, data.TargetPoint);
            ReflectionUtils.InvokePrivateMethod(typeof(MobilePartyAi), "UpdateBehavior", party.Ai);
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

            bool hasTarget = false;
            string targetId = string.Empty;

            if (targetPartyFigure != null)
            {
                hasTarget = true;
                targetId = targetPartyFigure.IsSettlement
                    ? targetPartyFigure.Settlement.StringId
                    : targetPartyFigure.MobileParty.StringId;
            }

            var data = new AiBehaviorUpdateData(party.StringId, newAiBehavior, hasTarget, targetId, bestTargetPoint);
            MessageBroker.Instance.Publish(__instance, new PartyAiBehaviorChanged(party, data));

            return false;
        }
    }
}
