using Common.Logging;
using Common.Network;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Smithing.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Smithing;

/// <summary>The server applies smithy requests only for a hero in the sending player's party.</summary>
public class SmithyRequestSenderTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private EnvironmentInstance Server => environment.Server;
    private EnvironmentInstance Owner => environment.Clients.First();

    private string ownerHeroId;
    private string otherHeroId;

    public SmithyRequestSenderTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        Server.Call(() =>
        {
            var ownerParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            Assert.True(Server.ObjectManager.TryGetId(ownerParty, out var ownerPartyId));
            Assert.True(Server.ObjectManager.TryGetId(ownerParty.LeaderHero, out ownerHeroId));
            Assert.True(Server.ObjectManager.TryGetId(GameObjectCreator.CreateInitializedObject<MobileParty>().LeaderHero, out otherHeroId));

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("Owner", ownerHeroId, ownerPartyId, null, null)));
            playerManager.SetPeer("Owner", Owner.NetPeer);

            // With no stamina left the smithy only reports the unchanged stamina, which shows the request was applied
            var smithy = new CraftingCampaignBehavior();
            Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(new CampaignBehaviorBase[] { smithy }));
            smithy.SetHeroCraftingStamina(Get<Hero>(otherHeroId), 0);
        });
        environment.FlushCoalescer();
        Server.NetworkSentMessages.Clear();
    }

    public void Dispose() => environment.Dispose();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SmeltingOrRefinementForAHeroOutsideTheSendersParty_IsRefused(bool refinement)
    {
        if (refinement)
            Send(new NetworkDoRefinement(otherHeroId, CraftingMaterials.IronOre, 0, CraftingMaterials.IronOre, 0, CraftingMaterials.Iron1, 0, CraftingMaterials.Iron1, 0));
        else
            Send(new NetworkDoSmelting(otherHeroId, default));

        Assert.Empty(Server.NetworkSentMessages.OfType<NetworkSetHeroCraftingStamina>());
    }

    // A refused crafting hero gets the usual failure reply, so the sender's client drops its pending crafted item.
    // The template doesn't exist either, so the refusal warning is what tells the refusal apart.
    [Fact]
    public void WeaponCraftingForAHeroOutsideTheSendersParty_RepliesWithAFailure()
    {
        string otherHeroStringId = null;
        Server.Call(() => otherHeroStringId = Get<Hero>(otherHeroId).StringId);
        var refusals = new List<string>();
        void CaptureRefusal(string message)
        {
            if (message.Contains($"Ignoring a smithy request for \"{otherHeroStringId}\"")) refusals.Add(message);
        }

        OutputSinkManager.AddLogCallback(CaptureRefusal);
        try
        {
            Send(new NetworkCreateCraftedWeaponInternalServer(true, otherHeroId,
                new TextObject("Test Sword"), null, "missing_template", "Test Sword", new List<string>(), new List<int>(),
                "", ownerHeroId, null, null, null, "request"));
            Server.Call(() => Server.Resolve<INetwork>().FlushPendingMessages());
        }
        finally
        {
            OutputSinkManager.RemoveLogCallback(CaptureRefusal);
        }

        Assert.Single(refusals);
        var reply = Assert.Single(Owner.InternalMessages.OfType<NetworkCreateCraftedWeaponInternalClients>());
        Assert.False(reply.Success);
        Assert.Equal("request", reply.ClientRequestId);
    }

    private void Send<T>(T message) where T : Common.Messaging.IMessage
    {
        Server.SimulateMessage(Owner.NetPeer, message);
        Server.PumpGameThread();
    }

    private T Get<T>(string id) where T : class => Server.GetRegisteredObject<T>(id);
}
