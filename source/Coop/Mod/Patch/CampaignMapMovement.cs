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

        private static readonly PropertyPatcher _MobilePartyPatcher =
            new PropertyPatcher(typeof(MobileParty))
                .Setter(nameof(MobileParty.DefaultBehavior))
                .Setter(nameof(MobileParty.TargetSettlement))
                .Setter(nameof(MobileParty.TargetParty))
                .Setter(nameof(MobileParty.TargetPosition));

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
            FieldChangeBuffer.TrackChanges(
                Movement,
                _MobilePartyPatcher.Setters,
                () => Coop.DoSync);
        }
    }
}
