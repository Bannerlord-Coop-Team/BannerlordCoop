using System.Collections.Generic;
using System.Reflection;
using Coop.Mod.Persistence;
using HarmonyLib;
using NLog;
using Sync;
using Sync.Attributes;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Library;
using Logger = NLog.Logger;

namespace Coop.Mod.Patch
{
    [Patch]
    public static class CampaignMapMovement
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static SyncFieldGroup<MobileParty, MovementData> Movement { get; } =
            new SyncFieldGroup<MobileParty, MovementData>(
                new List<SyncField>
                {
                    new SyncField<MobileParty, AiBehavior>(
                        AccessTools.Field(typeof(MobileParty), "_defaultBehavior")),
                    new SyncField<MobileParty, Settlement>(
                        AccessTools.Field(typeof(MobileParty), "_targetSettlement")),
                    new SyncField<MobileParty, MobileParty>(
                        AccessTools.Field(typeof(MobileParty), "_targetParty")),
                    new SyncField<MobileParty, Vec2>(
                        AccessTools.Field(typeof(MobileParty), "_targetPosition")),
                    new SyncField<MobileParty, int>(
                        AccessTools.Field(typeof(MobileParty), "_numberOfFleeingsAtLastTravel"))
                });

        [SyncWatch(typeof(MobileParty), nameof(MobileParty.DefaultBehavior), MethodType.Setter)]
        [SyncWatch(typeof(MobileParty), nameof(MobileParty.TargetSettlement), MethodType.Setter)]
        [SyncWatch(typeof(MobileParty), nameof(MobileParty.TargetParty), MethodType.Setter)]
        [SyncWatch(typeof(MobileParty), nameof(MobileParty.TargetPosition), MethodType.Setter)]
        private static void Patch_Movement(MobileParty __instance)
        {
            if (Coop.DoSync)
            {
                Movement.Watch(__instance);
            }
        }

        #region EnterSettlement
        private static readonly MethodInfo m_EnterSettlement_ApplyForParty = AccessTools.Method(
            typeof(EnterSettlementAction),
            nameof(EnterSettlementAction.ApplyForParty));

        public static SyncMethod EnterSettlement_ApplyForParty =
            new SyncMethod(m_EnterSettlement_ApplyForParty);

        [SyncCall(typeof(EnterSettlementAction), nameof(EnterSettlementAction.ApplyForParty))]
        private static bool Patch_EnterSettlement(MobileParty owner, Settlement settlement)
        {
            if (Coop.DoSync)
            {
                return EnterSettlement_ApplyForParty.RequestCall(
                    null,
                    new object[] {owner, settlement});
            }

            return true;
        }
        #endregion
    }
}
