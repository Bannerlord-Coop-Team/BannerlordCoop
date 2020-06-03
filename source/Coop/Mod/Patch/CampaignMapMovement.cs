using Coop.Mod.Persistence;
using HarmonyLib;
using NLog;
using Sync;
using Sync.Attributes;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using Logger = NLog.Logger;

namespace Coop.Mod.Patch
{
    [Patch]
    public static class CampaignMapMovement
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static FieldAccessGroup<MobileParty, MovementData> Movement { get; } =
            new FieldAccessGroup<MobileParty, MovementData>()
                .AddField<AiBehavior>("_defaultBehavior")
                .AddField<Settlement>("_targetSettlement")
                .AddField<MobileParty>("_targetParty")
                .AddField<Vec2>("_targetPosition")
                .AddField<int>("_numberOfFleeingsAtLastTravel");

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
    }
}
