using E2E.Tests.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Rafts;

/// <summary>
/// The AI enable and disable inside the server's raft action reach clients through the synced MobilePartyAi._isDisabled.
/// </summary>
public class RaftStateChangeActionTests : SyncTestBase
{
    private readonly string partyId;

    // Raft activation moves the party to the closest land, which needs the navigation mesh
    private static readonly MethodBase[] RaftDisabledMethods =
    {
        AccessTools.Method(typeof(MobileParty), nameof(MobileParty.MovePartyToTheClosestLand)),
    };

    /// <summary>Creates a registered party on every instance.</summary>
    public RaftStateChangeActionTests(ITestOutputHelper output) : base(output)
    {
        partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
    }

    // Starting a raft disables the AI so the owner's orders are ignored like vanilla, and landing must enable it again, otherwise it never moves
    [Fact]
    public void RaftState_ActivateThenDeactivate_SyncsTheAiEverywhere()
    {
        Server.Call(
            () => RaftStateChangeAction.ActivateRaftStateForParty(Server.GetRegisteredObject<MobileParty>(partyId)),
            RaftDisabledMethods);

        AssertRaftStateEverywhere(true);

        // The client gate is queued by RaftSync instead of an attribute, so a client's own landing must leave its raft alone
        var client = Clients.First();
        client.Call(() => RaftStateChangeAction.DeactivateRaftStateForParty(client.GetRegisteredObject<MobileParty>(partyId)));

        AssertRaftStateEverywhere(true);

        Server.Call(() => RaftStateChangeAction.DeactivateRaftStateForParty(Server.GetRegisteredObject<MobileParty>(partyId)));

        AssertRaftStateEverywhere(false);
    }

    // Checks the server's and every client's copy of the party's raft and AI state against the expected raft state.
    private void AssertRaftStateEverywhere(bool expected)
    {
        Server.Call(() => AssertRaftState(expected, Server.GetRegisteredObject<MobileParty>(partyId)));

        foreach (var client in Clients)
        {
            client.PumpGameThread();
            client.Call(() => AssertRaftState(expected, client.GetRegisteredObject<MobileParty>(partyId)));
        }
    }

    // A rafting party has its AI disabled and a landed one has it enabled.
    private static void AssertRaftState(bool expected, MobileParty party)
    {
        Assert.Equal(expected, party.IsInRaftState);
        Assert.Equal(expected, party.Ai.IsDisabled);
    }
}
