using Common.Network;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.UI.Notifications.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Companions;

/// <summary>
/// Tests that a non-host client calling a bare <see cref="RemoveCompanionAction"/> overload directly
/// (rather than through a dedicated request/response flow like <c>CompanionFired</c>) forwards to the
/// server instead of silently no-opping, and that the resulting removal converges to every client with
/// exactly one notification. Covers <see cref="GameInterface.Services.Actions.Patches.RemoveCompanionActionPatches"/>
/// and the accompanying fix to <see cref="GameInterface.Services.UI.Notifications.Patches.RemoveCompanionActionPatch"/>.
/// </summary>
public class CompanionDirectRemovalSyncTests : IDisposable
{
    private readonly E2ETestEnvironment testEnvironment;
    private EnvironmentInstance Server => testEnvironment.Server;
    private IReadOnlyList<EnvironmentInstance> Clients => testEnvironment.Clients.ToList();

    public CompanionDirectRemovalSyncTests(ITestOutputHelper output)
    {
        testEnvironment = new E2ETestEnvironment(output);
    }

    [Theory]
    [InlineData(RemoveCompanionAction.RemoveCompanionDetail.AfterQuest)]
    [InlineData(RemoveCompanionAction.RemoveCompanionDetail.Fire)]
    public void NonHostClient_CallingApplyDirectly_ForwardsToServerAndConvergesWithSingleNotification(
        RemoveCompanionAction.RemoveCompanionDetail detail)
    {
        var context = CreateCompanion();
        var requester = Clients[0];

        requester.Call(() =>
        {
            Assert.True(requester.ObjectManager.TryGetObject<Clan>(context.ClanId, out var clan));
            Assert.True(requester.ObjectManager.TryGetObject<Hero>(context.HeroId, out var companion));

            switch (detail)
            {
                case RemoveCompanionAction.RemoveCompanionDetail.Fire:
                    RemoveCompanionAction.ApplyByFire(clan, companion);
                    break;
                default:
                    RemoveCompanionAction.ApplyAfterQuest(clan, companion);
                    break;
            }
        });

        // The server is authoritative and ends up with the companion actually removed.
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(context.HeroId, out var companion));
            Assert.Null(companion.CompanionOf);
        });

        // Every client (the requester and any others) converges on the same state.
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(context.HeroId, out var companion));
                Assert.Null(companion.CompanionOf);
            });
        }

        // The client's bare Apply* call reached the server exactly once as a request, rather than
        // being silently swallowed client-side.
        Assert.Single(Server.InternalMessages.OfType<NetworkCompanionRemovalAttempted>());

        // The notification fired exactly once - on the server, after it actually applied the removal -
        // not spuriously on the requester's own locally-blocked attempt.
        Assert.Single(Server.NetworkSentMessages.OfType<NetworkNotifyCompanionRemoved>());
        Assert.Empty(requester.NetworkSentMessages.OfType<NetworkNotifyCompanionRemoved>());

        // The request-then-notify ordering holds: the server only announces the removal after it has
        // received and processed the forwarded attempt.
        var serverTranscript = Server.InternalMessages.ToList();
        int attemptIndex = serverTranscript.FindIndex(message => message is NetworkCompanionRemovalAttempted);
        int notifyIndex = serverTranscript.FindIndex(message => message is NotifyCompanionRemoved);
        Assert.True(attemptIndex >= 0);
        Assert.True(notifyIndex > attemptIndex);
    }

    [Fact]
    public void NonHostClient_CallingApplyForStaleCompanion_DoesNotForwardOrNotify()
    {
        // Removing a hero that is not (or no longer) this clan's companion must stay a local no-op on
        // the client - no request should reach the server and no notification should fire.
        var context = CreateCompanion();
        var requester = Clients[0];

        requester.Call(() =>
        {
            Assert.True(requester.ObjectManager.TryGetObject<Hero>(context.HeroId, out var companion));

            var otherClan = GameObjectCreator.CreateInitializedObject<Clan>();

            RemoveCompanionAction.ApplyAfterQuest(otherClan, companion);
        });

        Assert.Empty(Server.InternalMessages.OfType<NetworkCompanionRemovalAttempted>());
        Assert.Empty(Server.NetworkSentMessages.OfType<NetworkNotifyCompanionRemoved>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(context.HeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(context.ClanId, out var clan));
            Assert.Same(clan, companion.CompanionOf);
        });
    }

    private CompanionContext CreateCompanion()
    {
        string clanId = null;
        string heroId = null;

        Server.Call(() =>
        {
            var clan = GameObjectCreator.CreateInitializedObject<Clan>();
            var companion = GameObjectCreator.CreateInitializedObject<Hero>();

            var companionName = new TextObject("Direct Removal Test Companion");
            companion.SetName(companionName, companionName);

            AddCompanionAction.Apply(clan, companion);

            Assert.True(Server.ObjectManager.TryGetId(clan, out clanId));
            Assert.True(Server.ObjectManager.TryGetId(companion, out heroId));
        });
        testEnvironment.FlushCoalescer();

        return new CompanionContext(clanId, heroId);
    }

    private readonly record struct CompanionContext(string ClanId, string HeroId);

    public void Dispose() => testEnvironment.Dispose();
}
