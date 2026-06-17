using GameInterface.Services.GuantletMapEventVisuals.Patches;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using Xunit;
using FormatterServices = System.Runtime.Serialization.FormatterServices;

namespace GameInterface.Tests.Services.GuantletMapEventVisuals
{
    /// <summary>
    /// Tests the not-yet-ready guard in <see cref="GauntletMapEventVisualPatches"/> that keeps the
    /// field-battle battle-size calc from dereferencing a client map event whose sides - or the
    /// parties within them - have not finished syncing.
    /// </summary>
    public class GauntletMapEventVisualPatchesTests
    {
        private static readonly FieldInfo SidesField =
            typeof(MapEvent).GetField("_sides", BindingFlags.NonPublic | BindingFlags.Instance)!;
        private static readonly FieldInfo BattlePartiesField =
            typeof(MapEventSide).GetField("_battleParties", BindingFlags.NonPublic | BindingFlags.Instance)!;
        private static readonly PropertyInfo PartyProperty =
            typeof(MapEventParty).GetProperty(nameof(MapEventParty.Party))!;

        // DefenderSide reads _sides[0]; AttackerSide reads _sides[1].
        private static MapEvent CreateMapEvent(MapEventSide? defender, MapEventSide? attacker)
        {
            MapEvent mapEvent = (MapEvent)FormatterServices.GetUninitializedObject(typeof(MapEvent));
            SidesField.SetValue(mapEvent, new[] { defender, attacker });
            return mapEvent;
        }

        // Sides are created via GetUninitializedObject on the client too, so give each one a real
        // _battleParties list (the registry reinitializes it before the side is used) to mirror runtime.
        private static MapEventSide CreateSide(params MapEventParty[] parties)
        {
            MapEventSide side = (MapEventSide)FormatterServices.GetUninitializedObject(typeof(MapEventSide));
            BattlePartiesField.SetValue(side, new MBList<MapEventParty>(parties));
            return side;
        }

        // resolved = the party's underlying Party reference has synced (non-null); otherwise it is the
        // transient state where the MapEventParty exists in the side but its Party has not arrived yet.
        private static MapEventParty CreateParty(bool resolved)
        {
            MapEventParty party = (MapEventParty)FormatterServices.GetUninitializedObject(typeof(MapEventParty));
            if (resolved)
                PartyProperty.SetValue(party, (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase)));
            return party;
        }

        [Fact]
        public void BattleSizeComputable_NullMapEvent_ReturnsFalse()
        {
            Assert.False(GauntletMapEventVisualPatches.BattleSizeComputable(null));
        }

        [Fact]
        public void BattleSizeComputable_BothSidesNull_ReturnsFalse()
        {
            MapEvent mapEvent = CreateMapEvent(defender: null, attacker: null);

            Assert.False(GauntletMapEventVisualPatches.BattleSizeComputable(mapEvent));
        }

        [Fact]
        public void BattleSizeComputable_DefenderSideNull_ReturnsFalse()
        {
            MapEvent mapEvent = CreateMapEvent(defender: null, attacker: CreateSide());

            Assert.False(GauntletMapEventVisualPatches.BattleSizeComputable(mapEvent));
        }

        [Fact]
        public void BattleSizeComputable_AttackerSideNull_ReturnsFalse()
        {
            MapEvent mapEvent = CreateMapEvent(defender: CreateSide(), attacker: null);

            Assert.False(GauntletMapEventVisualPatches.BattleSizeComputable(mapEvent));
        }

        [Fact]
        public void BattleSizeComputable_SidesPresentButPartyUnresolved_ReturnsFalse()
        {
            MapEvent mapEvent = CreateMapEvent(
                defender: CreateSide(CreateParty(resolved: true)),
                attacker: CreateSide(CreateParty(resolved: false)));

            Assert.False(GauntletMapEventVisualPatches.BattleSizeComputable(mapEvent));
        }

        [Fact]
        public void BattleSizeComputable_SidesPresentNoParties_ReturnsTrue()
        {
            MapEvent mapEvent = CreateMapEvent(defender: CreateSide(), attacker: CreateSide());

            Assert.True(GauntletMapEventVisualPatches.BattleSizeComputable(mapEvent));
        }

        [Fact]
        public void BattleSizeComputable_SidesPresentAllPartiesResolved_ReturnsTrue()
        {
            MapEvent mapEvent = CreateMapEvent(
                defender: CreateSide(CreateParty(resolved: true)),
                attacker: CreateSide(CreateParty(resolved: true), CreateParty(resolved: true)));

            Assert.True(GauntletMapEventVisualPatches.BattleSizeComputable(mapEvent));
        }
    }
}
