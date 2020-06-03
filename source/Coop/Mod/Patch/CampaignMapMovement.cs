using Coop.Mod.Persistence;
using NLog;
using Sync;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using Logger = NLog.Logger;

namespace Coop.Mod.Patch
{
    public static class CampaignMapMovement
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static readonly PropertyPatch MobilePartyPatch =
            new PropertyPatch(typeof(MobileParty))
                .RelaySetter(nameof(MobileParty.DefaultBehavior))
                .RelaySetter(nameof(MobileParty.TargetSettlement))
                .RelaySetter(nameof(MobileParty.TargetParty))
                .RelaySetter(nameof(MobileParty.TargetPosition));

        public static FieldAccessGroup<MobileParty, MovementData> Movement { get; } =
            new FieldAccessGroup<MobileParty, MovementData>()
                .AddField<AiBehavior>("_defaultBehavior")
                .AddField<Settlement>("_targetSettlement")
                .AddField<MobileParty>("_targetParty")
                .AddField<Vec2>("_targetPosition")
                .AddField<int>("_numberOfFleeingsAtLastTravel");

        [PatchInitializer]
        public static void Init()
        {
            FieldChangeBuffer.TrackChanges(Movement, MobilePartyPatch.Setters, () => Coop.DoSync);
        }
    }
}
