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

    /// <summary>Creates a registered party on every instance.</summary>
    public RaftStateChangeActionTests(ITestOutputHelper output) : base(output)
    {
        partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
    }

    // A party loaded on a raft has its AI disabled everywhere, and landing must enable it on its owner, otherwise it never moves again
    [Fact]
    public void DeactivateRaftState_EnablesTheAiOnClients()
    {
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            party.Ai.DisableAi();
            party.IsInRaftState = true;
        });
        AssertClientsAiDisabled(true);

        Server.Call(() => RaftStateChangeAction.DeactivateRaftStateForParty(Server.GetRegisteredObject<MobileParty>(partyId)));

        AssertClientsAiDisabled(false);
    }

    // Starting a raft disables the AI, which the owner's client needs too so its orders are ignored like vanilla
    [Fact]
    public void ActivateRaftState_DisablesTheAiOnClients()
    {
        Server.Call(
            () => RaftStateChangeAction.ActivateRaftStateForParty(Server.GetRegisteredObject<MobileParty>(partyId)),
            RaftDisabledMethods);

        AssertClientsAiDisabled(true);
    }

    // Raft activation moves the party to the closest land, which needs the navigation mesh
    private static readonly MethodBase[] RaftDisabledMethods =
    {
        AccessTools.Method(typeof(MobileParty), nameof(MobileParty.MovePartyToTheClosestLand)),
    };

    // Checks every client's copy of the party's AI against the expected disabled state.
    private void AssertClientsAiDisabled(bool expected)
    {
        foreach (var client in Clients)
        {
            client.PumpGameThread();
            client.Call(() => Assert.Equal(expected, client.GetRegisteredObject<MobileParty>(partyId).Ai.IsDisabled));
        }
    }
}
