using System.Collections.Generic;
using Coop.Mod.Persistence;
using Coop.Sync;
using HarmonyLib;
using NLog;
using TaleWorlds.CampaignSystem;
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
            Movement.Watch(__instance);
        }
    }
}
