using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.UI.Notifications.Messages;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
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

    private readonly Harmony harmony = new Harmony("e2e.notifications.null-lists");
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Client => TestEnvironment.Clients.First();

    public DefaultNotificationsHandlerNullListTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
        Births.Clear();
        JoinedArmyExceptions.Clear();
        joinedArmyCalls = 0;

        harmony.Patch(AccessTools.Method(typeof(DefaultNotificationsCampaignBehavior), "OnGivenBirth"),
            postfix: new HarmonyMethod(typeof(DefaultNotificationsHandlerNullListTests), nameof(RecordBirth)));
        harmony.Patch(AccessTools.Method(typeof(DefaultNotificationsCampaignBehavior), "OnPartyJoinedArmy"),
            prefix: new HarmonyMethod(typeof(DefaultNotificationsHandlerNullListTests), nameof(CountJoinedArmy)),
            finalizer: new HarmonyMethod(typeof(DefaultNotificationsHandlerNullListTests), nameof(RecordJoinedArmyException)));

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

    private static void CountJoinedArmy() => joinedArmyCalls++;

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
    public void GivenBirth_UnresolvableChild_StillNotifiesTheResolvedOnes()
    {
        string motherId = TestEnvironment.CreateRegisteredObject<Hero>();
        string childId = TestEnvironment.CreateRegisteredObject<Hero>();
        var message = Client.EnsureSerializable(
            new NetworkNotifyGivenBirth(motherId, new List<string> { "Hero_missing", childId }, 0));

        Client.SimulateMessage(this, message);
        Client.PumpGameThread();

        Assert.Equal(1, Assert.Single(Births).Offspring);
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
}
