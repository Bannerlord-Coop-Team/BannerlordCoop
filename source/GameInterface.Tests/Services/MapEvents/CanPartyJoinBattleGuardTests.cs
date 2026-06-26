using GameInterface.Services.MapEvents.Patches;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using Xunit;
using FormatterServices = System.Runtime.Serialization.FormatterServices;

namespace GameInterface.Tests.Services.MapEvents
{
    /// <summary>
    /// Tests the unresolved-party guard in <see cref="InteractionPatches.CanEvaluateJoinBattle"/>. Vanilla
    /// MapEvent.CanPartyJoinBattle - run by the diplomacy-continuity sweep when a war/peace stance change
    /// fires - walks both sides and dereferences the passed party's MapFaction and every MapEventParty's
    /// Party and Party.MapFaction. On a receiving co-op machine a MapEventParty can be in a side before its
    /// Party/MapFaction has synced, so the check throws a NullReferenceException (issue #1538). The guard
    /// returns false while any of that is unresolved so the prefix can skip vanilla instead of crashing.
    ///
    /// TaleWorlds.CampaignSystem is publicized for this test assembly, so the otherwise-private backing
    /// members Party, MobileParty and _actualClan are assigned directly; _sides and _battleParties are
    /// readonly fields, so they are set by reflection.
    /// </summary>
    public class CanPartyJoinBattleGuardTests
    {
        private static readonly FieldInfo SidesField =
            typeof(MapEvent).GetField("_sides", BindingFlags.NonPublic | BindingFlags.Instance)!;
        private static readonly FieldInfo BattlePartiesField =
            typeof(MapEventSide).GetField("_battleParties", BindingFlags.NonPublic | BindingFlags.Instance)!;

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

        // partySet = the MapEventParty's underlying Party has synced (non-null). factionResolved = that
        // Party also resolves a non-null MapFaction. The two false cases are the transient states the guard
        // must catch: the Party has not arrived yet, or it has but its faction has not.
        private static MapEventParty CreateParty(bool partySet, bool factionResolved)
        {
            MapEventParty mapEventParty = (MapEventParty)FormatterServices.GetUninitializedObject(typeof(MapEventParty));
            if (partySet)
                mapEventParty.Party = CreatePartyBase(factionResolved);
            return mapEventParty;
        }

        private static MapEventParty ResolvedParty() => CreateParty(partySet: true, factionResolved: true);

        // PartyBase.MapFaction returns MobileParty.MapFaction (then Settlement.MapFaction) or null. A
        // MobileParty whose ActualClan is set resolves MapFaction to that clan (Clan.MapFaction returns the
        // clan itself when it is in no kingdom), with no Campaign needed; leaving MobileParty null makes
        // PartyBase.MapFaction return null, the unresolved-faction case. (PartyBase's MobileParty-taking
        // constructor calls Campaign.Current, so build it uninitialized and set the backing directly.)
        private static PartyBase CreatePartyBase(bool factionResolved)
        {
            PartyBase partyBase = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));
            if (factionResolved)
            {
                MobileParty mobileParty = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
                mobileParty._actualClan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
                partyBase.MobileParty = mobileParty;
            }
            return partyBase;
        }

        [Fact]
        public void NullMapEvent_CannotEvaluate()
        {
            Assert.False(InteractionPatches.CanEvaluateJoinBattle(null, CreatePartyBase(factionResolved: true)));
        }

        [Fact]
        public void DefenderSideNull_CannotEvaluate()
        {
            MapEvent mapEvent = CreateMapEvent(defender: null, attacker: CreateSide());

            Assert.False(InteractionPatches.CanEvaluateJoinBattle(mapEvent, CreatePartyBase(factionResolved: true)));
        }

        [Fact]
        public void AttackerSideNull_CannotEvaluate()
        {
            MapEvent mapEvent = CreateMapEvent(defender: CreateSide(), attacker: null);

            Assert.False(InteractionPatches.CanEvaluateJoinBattle(mapEvent, CreatePartyBase(factionResolved: true)));
        }

        [Fact]
        public void PassedPartyNull_CannotEvaluate()
        {
            MapEvent mapEvent = CreateMapEvent(defender: CreateSide(), attacker: CreateSide());

            Assert.False(InteractionPatches.CanEvaluateJoinBattle(mapEvent, null));
        }

        [Fact]
        public void PassedPartyUnresolvedFaction_CannotEvaluate()
        {
            MapEvent mapEvent = CreateMapEvent(defender: CreateSide(), attacker: CreateSide());

            Assert.False(InteractionPatches.CanEvaluateJoinBattle(mapEvent, CreatePartyBase(factionResolved: false)));
        }

        [Fact]
        public void SidePartyUnresolved_CannotEvaluate()
        {
            MapEvent mapEvent = CreateMapEvent(
                defender: CreateSide(ResolvedParty()),
                attacker: CreateSide(CreateParty(partySet: false, factionResolved: false)));

            Assert.False(InteractionPatches.CanEvaluateJoinBattle(mapEvent, CreatePartyBase(factionResolved: true)));
        }

        [Fact]
        public void SidePartyUnresolvedFaction_CannotEvaluate()
        {
            MapEvent mapEvent = CreateMapEvent(
                defender: CreateSide(ResolvedParty()),
                attacker: CreateSide(CreateParty(partySet: true, factionResolved: false)));

            Assert.False(InteractionPatches.CanEvaluateJoinBattle(mapEvent, CreatePartyBase(factionResolved: true)));
        }

        // An empty-but-present side has no parties to dereference, so the check can run (vanilla walks zero
        // parties). The guard asks only whether the check can run without throwing, not whether the battle
        // is fully populated.
        [Fact]
        public void SidesPresentNoParties_CanEvaluate()
        {
            MapEvent mapEvent = CreateMapEvent(defender: CreateSide(), attacker: CreateSide());

            Assert.True(InteractionPatches.CanEvaluateJoinBattle(mapEvent, CreatePartyBase(factionResolved: true)));
        }

        [Fact]
        public void SidesPresentAllPartiesResolved_CanEvaluate()
        {
            MapEvent mapEvent = CreateMapEvent(
                defender: CreateSide(ResolvedParty()),
                attacker: CreateSide(ResolvedParty(), ResolvedParty()));

            Assert.True(InteractionPatches.CanEvaluateJoinBattle(mapEvent, CreatePartyBase(factionResolved: true)));
        }
    }
}
