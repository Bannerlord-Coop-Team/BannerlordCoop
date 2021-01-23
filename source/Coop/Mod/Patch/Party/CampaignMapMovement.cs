using Coop.Mod.Persistence;
using Coop.Mod.Persistence.Party;
using CoopFramework;
using Sync;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace Coop.Mod.Patch
{
    public static class CampaignMapMovement
    {
        private class TPatch
        {
            // TODO: Replace TimeControl with a CoopManaged
        }
        private static readonly PropertyPatch<TPatch> MobilePartyPatch =
            new PropertyPatch<TPatch>(typeof(MobileParty))
                .InterceptSetter(nameof(MobileParty.DefaultBehavior))
                .InterceptSetter(nameof(MobileParty.TargetSettlement))
                .InterceptSetter(nameof(MobileParty.TargetParty))
                .InterceptSetter(nameof(MobileParty.TargetPosition));

        public static FieldAccessGroup<MobileParty, MovementData> Movement { get; } =
            new FieldAccessGroup<MobileParty, MovementData>()
                .AddField<AiBehavior>("_defaultBehavior")
                .AddField<Settlement>("_targetSettlement")
                .AddField<MobileParty>("_targetParty")
                .AddField<Vec2>("_targetPosition")
                .AddField<int>("_numberOfFleeingsAtLastTravel");

        [PatchInitializer]
        public static void Init(ISynchronization sync)
        {
            sync.RegisterSyncedField(Movement, MobilePartyPatch.Setters, Coop.DoSync);
        }
    }
}
