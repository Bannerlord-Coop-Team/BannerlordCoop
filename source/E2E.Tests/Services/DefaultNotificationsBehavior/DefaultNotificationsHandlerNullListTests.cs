using Common;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.UI.Notifications.Messages;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.DefaultNotificationsBehavior;

/// <summary>
/// Client notification handlers apply messages whose lists can arrive null (protobuf writes nothing for an empty
/// list) and must not hand vanilla a state it dereferences without a check.
/// </summary>
[Collection(nameof(DefaultNotificationsHandlerNullListTests))]
public class DefaultNotificationsHandlerNullListTests : IDisposable
{
    private static readonly List<(Hero Mother, int Offspring, int Stillborn)> Births = new();
    private static readonly List<Exception> JoinedArmyExceptions = new();
    private static int joinedArmyCalls;
    private static int attachedPartyCalls;
    private static readonly List<int> TournamentParticipantCounts = new();

    private readonly Harmony harmony = new Harmony("e2e.notifications.null-lists");
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Client => TestEnvironment.Clients.First();

    public DefaultNotificationsHandlerNullListTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
        Births.Clear();
        JoinedArmyExceptions.Clear();
        joinedArmyCalls = 0;
        attachedPartyCalls = 0;
        TournamentParticipantCounts.Clear();

        harmony.Patch(AccessTools.Method(typeof(DefaultNotificationsCampaignBehavior), "OnGivenBirth"),
            postfix: new HarmonyMethod(typeof(DefaultNotificationsHandlerNullListTests), nameof(RecordBirth)));
        harmony.Patch(AccessTools.Method(typeof(DefaultNotificationsCampaignBehavior), "OnPartyJoinedArmy"),
            prefix: new HarmonyMethod(typeof(DefaultNotificationsHandlerNullListTests), nameof(CountJoinedArmy)),
            finalizer: new HarmonyMethod(typeof(DefaultNotificationsHandlerNullListTests), nameof(RecordJoinedArmyException)));
        harmony.Patch(AccessTools.Method(typeof(DefaultNotificationsCampaignBehavior), "OnPartyAttachedAnotherParty"),
            prefix: new HarmonyMethod(typeof(DefaultNotificationsHandlerNullListTests), nameof(CountAttachedParty)),
            finalizer: new HarmonyMethod(typeof(DefaultNotificationsHandlerNullListTests), nameof(RecordJoinedArmyException)));
        harmony.Patch(AccessTools.Method(typeof(DefaultNotificationsCampaignBehavior), "OnTournamentFinished"),
            prefix: new HarmonyMethod(typeof(DefaultNotificationsHandlerNullListTests), nameof(RecordTournamentFinished)));

        Client.Call(() => Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(
            new CampaignBehaviorBase[] { new DefaultNotificationsCampaignBehavior() })));
    }

    public void Dispose()
    {
        harmony.UnpatchAll(harmony.Id);
        TestEnvironment.Dispose();
    }

    private static void RecordBirth(Hero mother, List<Hero> aliveOffsprings, int stillbornCount)
        => Births.Add((mother, aliveOffsprings.Count, stillbornCount));

    // The server only has to send the notification on, so it skips the vanilla text there
    private static bool CountJoinedArmy()
    {
        if (ModInformation.IsServer) return false;

        joinedArmyCalls++;
        return true;
    }

    private static void CountAttachedParty() => attachedPartyCalls++;

    // Skips the vanilla text, the test only needs to see the handler got this far
    private static bool RecordTournamentFinished(MBReadOnlyList<CharacterObject> participants)
    {
        TournamentParticipantCounts.Add(participants.Count);
        return false;
    }

    private static Exception RecordJoinedArmyException(Exception __exception)
    {
        if (__exception != null) JoinedArmyExceptions.Add(__exception);
        return __exception;
    }

    [Fact]
    public void GivenBirth_StillbornOnly_ArrivesWithANullListAndStillNotifies()
    {
        string motherId = TestEnvironment.CreateRegisteredObject<Hero>();
        var message = Client.EnsureSerializable(new NetworkNotifyGivenBirth(motherId, new List<string>(), 1));
        Assert.Null(message.AliveOffspringsIds);

        Client.SimulateMessage(this, message);
        Client.PumpGameThread();

        var birth = Assert.Single(Births);
        Assert.Equal(0, birth.Offspring);
        Assert.Equal(1, birth.Stillborn);
    }

    [Fact]
    public void GivenBirth_UnresolvableChild_SkipsInsteadOfShowingTheWrongCount()
    {
        string motherId = TestEnvironment.CreateRegisteredObject<Hero>();
        string childId = TestEnvironment.CreateRegisteredObject<Hero>();
        var message = Client.EnsureSerializable(
            new NetworkNotifyGivenBirth(motherId, new List<string> { "Hero_missing", childId }, 0));

        Client.SimulateMessage(this, message);
        Client.PumpGameThread();

        Assert.Empty(Births);
    }

    [Fact]
    public void GivenBirth_ResolvedChild_Notifies()
    {
        string motherId = TestEnvironment.CreateRegisteredObject<Hero>();
        string childId = TestEnvironment.CreateRegisteredObject<Hero>();
        var message = Client.EnsureSerializable(new NetworkNotifyGivenBirth(motherId, new List<string> { childId }, 0));

        Client.SimulateMessage(this, message);
        Client.PumpGameThread();

        var birth = Assert.Single(Births);
        Assert.Equal(1, birth.Offspring);
        Assert.Equal(0, birth.Stillborn);
    }

    [Fact]
    public void TournamentFinished_NoParticipants_ArrivesWithANullListAndStillNotifies()
    {
        string winnerId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        string townId = TestEnvironment.CreateRegisteredObject<Town>();
        string prizeId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        var message = Client.EnsureSerializable(
            new NetworkNotifyTournamentFinished(winnerId, new MBReadOnlyList<string>(), townId, prizeId));
        Assert.Null(message.ParticipantsIds);

        Client.SimulateMessage(this, message);
        Client.PumpGameThread();

        Assert.Equal(0, Assert.Single(TournamentParticipantCounts));
    }

    [Fact]
    public void PartyJoinedArmy_PartyWithoutArmy_DoesNotThrow()
    {
        string partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Client.Call(() =>
        {
            Assert.Null(Client.GetRegisteredObject<MobileParty>(partyId).Army);
            Assert.Null(MobileParty.MainParty.Army);
        });

        Client.SimulateMessage(this, new NetworkNotifyPartyJoinedArmy(partyId));
        Client.PumpGameThread();

        Assert.Empty(JoinedArmyExceptions);
        Assert.Equal(0, joinedArmyCalls);
    }

    [Fact]
    public void PartyJoinedArmy_PartyInAnArmy_StillReachesVanilla()
    {
        string partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Client.Call(() => Client.GetRegisteredObject<MobileParty>(partyId)._army = ObjectHelper.SkipConstructor<Army>());

        Client.SimulateMessage(this, new NetworkNotifyPartyJoinedArmy(partyId));
        Client.PumpGameThread();

        Assert.Empty(JoinedArmyExceptions);
        Assert.Equal(1, joinedArmyCalls);
    }

    [Fact]
    public void PartyJoinedArmy_JoinAClientStarts_ReachesClientsAfterTheArmyAdd()
    {
        string armyId = TestEnvironment.CreateRegisteredObject<Army>();
        string partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        TestEnvironment.Server.Call(() =>
        {
            var behavior = new DefaultNotificationsCampaignBehavior();
            Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(new CampaignBehaviorBase[] { behavior }));
            behavior.RegisterEvents();
        });

        TestEnvironment.Server.SimulateMessage(this, new NetworkAddMobilePartyInArmy(armyId, partyId, false));
        TestEnvironment.Server.PumpGameThread();
        Client.PumpGameThread();

        var sent = TestEnvironment.Server.NetworkSentMessages.ToList();
        int addIndex = sent.FindIndex(message => message is NetworkAddMobilePartyInArmy add && add.MobilePartyId == partyId);
        int notifyIndex = sent.FindIndex(message => message is NetworkNotifyPartyJoinedArmy);
        Assert.True(addIndex >= 0 && notifyIndex > addIndex, $"add at {addIndex}, notification at {notifyIndex}");
        Assert.Empty(JoinedArmyExceptions);
        Assert.Equal(1, joinedArmyCalls);
    }

    [Fact]
    public void PartyAttachedAnotherParty_PartyWithoutArmy_DoesNotThrow()
    {
        string partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Client.Call(() =>
        {
            Assert.Null(Client.GetRegisteredObject<MobileParty>(partyId).Army);
            Assert.Null(MobileParty.MainParty.Army);
        });

        Client.SimulateMessage(this, new NetworkNotifyPartyAttachedAnotherParty(partyId));
        Client.PumpGameThread();

        Assert.Empty(JoinedArmyExceptions);
        Assert.Equal(0, attachedPartyCalls);
    }
}
