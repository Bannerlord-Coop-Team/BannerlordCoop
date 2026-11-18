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

        AssertAiDisabledEverywhere(true);

        Server.Call(() => RaftStateChangeAction.DeactivateRaftStateForParty(Server.GetRegisteredObject<MobileParty>(partyId)));

        AssertAiDisabledEverywhere(false);
    }

    // Checks the server's and every client's copy of the party's AI against the expected disabled state.
    private void AssertAiDisabledEverywhere(bool expected)
    {
        Server.Call(() => Assert.Equal(expected, Server.GetRegisteredObject<MobileParty>(partyId).Ai.IsDisabled));

        foreach (var client in Clients)
        {
            client.PumpGameThread();
            client.Call(() => Assert.Equal(expected, client.GetRegisteredObject<MobileParty>(partyId).Ai.IsDisabled));
        }
    }
}
