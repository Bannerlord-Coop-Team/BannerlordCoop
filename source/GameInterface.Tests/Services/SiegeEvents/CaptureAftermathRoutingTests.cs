using GameInterface.Services.SiegeEvents.Patches;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using Xunit;
using FormatterServices = System.Runtime.Serialization.FormatterServices;

namespace GameInterface.Tests.Services.SiegeEvents
{
    /// <summary>
    /// Tests the capture-aftermath backstop predicate in <see cref="EncounterCaptureAftermathInitPatch"/>.
    ///
    /// Repelling a garrison sally-out is a settlement CAPTURE in vanilla: MapEvent.FinalizeEventAux dispatches
    /// SiegeCompleted(settlement, defenderLeader, isWin: true, SallyOut) and KingdomManager.SiegeCompleted then
    /// runs RemoveAllSiegeParties + ChangeOwnerOfSettlementAction.ApplyBySiege. Co-op never calls
    /// PlayerEncounter.DoEnd, so this backstop is what routes the capturer to menu_settlement_taken.
    ///
    /// It used to demand PlayerEncounter.Battle be non-null AND IsSiegeAssault. Both fail for a sortie: the
    /// battle type is SallyOut, and after the server tears the map event down PlayerEncounter.Battle
    /// (== Current._mapEvent) is null anyway - a live capture of the stuck state showed _mapEvent: null. So the
    /// backstop was dead code for the exact case it needed to cover, and the player was stranded on a dead
    /// encounter menu whose only option did nothing.
    ///
    /// TaleWorlds.CampaignSystem is publicized for this assembly, so backing fields are set directly.
    /// </summary>
    public class CaptureAftermathRoutingTests
    {
        private static T Raw<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

        private static void SetField<T>(object target, string field, T value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var info = type.GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (info == null) continue;
                info.SetValue(target, value);
                return;
            }

            Assert.Fail($"field '{field}' not found on {target.GetType().Name} or its base types");
        }

        private static void SetBacking<T>(object target, string property, T value)
            => SetField(target, $"<{property}>k__BackingField", value);

        private static Clan CreateClan() => Raw<Clan>();

        private static MapEvent CreateBattle(MapEvent.BattleTypes type)
        {
            var battle = Raw<MapEvent>();
            SetField(battle, "_mapEventType", type);
            return battle;
        }

        /// <summary>
        /// A fortification captured by <paramref name="capturedBy"/>, with no siege left running.
        /// Settlement.IsFortification is derived (IsTown || IsCastle) and Town.IsTown is !_isCastle, so an
        /// uninitialized Town is already a town; and Settlement.OwnerClan delegates to Town._ownerClan.
        /// </summary>
        private static Settlement CreateCapturedFortification(Clan owner, Clan capturedBy)
        {
            var town = Raw<Town>();
            SetField(town, "_ownerClan", owner);
            SetBacking(town, "LastCapturedBy", capturedBy);

            var settlement = Raw<Settlement>();
            settlement.Town = town;
            return settlement;
        }

        private static MobileParty CreateParty(Settlement currentSettlement = null, BesiegerCamp camp = null)
        {
            var party = Raw<MobileParty>();
            SetField(party, "_currentSettlement", currentSettlement);
            SetField(party, "_besiegerCamp", camp);
            return party;
        }

        private static bool Invoke(MapEvent battle, Settlement settlement, Clan clan, MobileParty party)
            => EncounterCaptureAftermathInitPatch.IsStrandedCaptureEncounter(battle, settlement, clan, party);

        [Fact]
        public void SallyOutCapture_ByBesiegerOutsideTheTown_IsStranded()
        {
            var clan = CreateClan();
            var settlement = CreateCapturedFortification(owner: clan, capturedBy: clan);

            Assert.True(Invoke(CreateBattle(MapEvent.BattleTypes.SallyOut), settlement, clan, CreateParty()));
        }

        [Fact]
        public void SiegeAssaultCapture_StillStranded()
        {
            var clan = CreateClan();
            var settlement = CreateCapturedFortification(owner: clan, capturedBy: clan);

            Assert.True(Invoke(CreateBattle(MapEvent.BattleTypes.Siege), settlement, clan, CreateParty()));
        }

        [Fact]
        public void CapturerWhoIsNotTheirKingdomsRuler_IsStillRecognised()
        {
            // ApplyBySiege gives OwnerClan to the kingdom leader and records the real capturer in
            // Town.LastCapturedBy, so an OwnerClan-only test misses every non-ruler capturer.
            var capturer = CreateClan();
            var ruler = CreateClan();
            var settlement = CreateCapturedFortification(owner: ruler, capturedBy: capturer);

            Assert.True(Invoke(CreateBattle(MapEvent.BattleTypes.SallyOut), settlement, capturer, CreateParty()));
        }

        [Fact]
        public void FieldBattle_IsNotStranded()
        {
            var clan = CreateClan();
            var settlement = CreateCapturedFortification(owner: clan, capturedBy: clan);

            Assert.False(Invoke(CreateBattle(MapEvent.BattleTypes.FieldBattle), settlement, clan, CreateParty()));
        }

        [Fact]
        public void SettlementOwnedByAnotherClan_IsNotStranded()
        {
            var other = CreateClan();
            var settlement = CreateCapturedFortification(owner: other, capturedBy: other);

            Assert.False(Invoke(CreateBattle(MapEvent.BattleTypes.SallyOut), settlement, CreateClan(), CreateParty()));
        }

        [Fact]
        public void WinningDefenderInsideItsOwnTown_IsNotStranded()
        {
            var clan = CreateClan();
            var settlement = CreateCapturedFortification(owner: clan, capturedBy: clan);

            // Inside the settlement: that is the defender, which has its own victory prompt.
            Assert.False(Invoke(CreateBattle(MapEvent.BattleTypes.SallyOut), settlement, clan,
                CreateParty(currentSettlement: settlement)));
        }

        [Fact]
        public void SiegeStillRunning_IsNotStranded()
        {
            // A capture ends the siege, so a live siege means this is an ordinary siege menu, not an aftermath.
            var clan = CreateClan();
            var settlement = CreateCapturedFortification(owner: clan, capturedBy: clan);
            SetBacking(settlement, "SiegeEvent", Raw<SiegeEvent>());

            Assert.False(Invoke(CreateBattle(MapEvent.BattleTypes.SallyOut), settlement, clan, CreateParty()));
        }

        [Fact]
        public void StillBesieging_IsNotStranded()
        {
            var clan = CreateClan();
            var settlement = CreateCapturedFortification(owner: clan, capturedBy: clan);

            Assert.False(Invoke(CreateBattle(MapEvent.BattleTypes.SallyOut), settlement, clan,
                CreateParty(camp: Raw<BesiegerCamp>())));
        }

        [Fact]
        public void NullBattleWithNoPendingChoice_IsNotStranded()
        {
            // With the map event already torn down and no aftermath choice pending, there is no evidence a
            // capture happened - the backstop must not fire on every encounter outside one of our own towns.
            var clan = CreateClan();
            var settlement = CreateCapturedFortification(owner: clan, capturedBy: clan);

            Assert.False(Invoke(null, settlement, clan, CreateParty()));
        }

        [Fact]
        public void NullOrNonFortification_IsNotStranded()
        {
            var clan = CreateClan();

            // No Town component at all: a village, which is not a fortification.
            var village = Raw<Settlement>();
            Assert.False(Invoke(CreateBattle(MapEvent.BattleTypes.SallyOut), village, clan, CreateParty()));

            var settlement = CreateCapturedFortification(owner: clan, capturedBy: clan);
            Assert.False(Invoke(CreateBattle(MapEvent.BattleTypes.SallyOut), null, clan, CreateParty()));
            Assert.False(Invoke(CreateBattle(MapEvent.BattleTypes.SallyOut), settlement, null, CreateParty()));
        }
    }
}
