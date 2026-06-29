using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MobilePartyAIs.Handlers;
using GameInterface.Services.MobilePartyAIs.Messages;
using GameInterface.Services.ObjectManager;
using Moq;
using Serilog;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobilePartyAIs;

/// <summary>
/// Prevents tests that mutate shared synchronization and game-thread state from running in parallel.
/// </summary>
[CollectionDefinition(nameof(MobilePartyAiSyncCollection), DisableParallelization = true)]
public class MobilePartyAiSyncCollection
{
}

/// <summary>
/// Tests game-thread application of received mobile-party AI updates.
/// </summary>
[Collection(nameof(MobilePartyAiSyncCollection))]
public class MobilePartyAiHandlerTests
{
    static MobilePartyAiHandlerTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void UpdateInteractable_ResolvesAndAppliesOnGameThread()
    {
        Assert.True(GameThread.Instance.IsInitialized, "game-loop pump was not initialized");

        var objectManager = new ObjectManager(new LoggerConfiguration().CreateLogger());
        var handler = new MobilePartyAiHandler(
            new Mock<IMessageBroker>().Object,
            objectManager,
            new Mock<INetwork>().Object);
        var partyAi = ObjectHelper.SkipConstructor<MobilePartyAi>();
        var interactablePoint = ObjectHelper.SkipConstructor<PartyBase>();
        var gameThreadBlocked = new ManualResetEventSlim(false);
        var releaseGameThread = new ManualResetEventSlim(false);

        GameThread.Run(() =>
        {
            gameThreadBlocked.Set();
            releaseGameThread.Wait(TimeSpan.FromSeconds(10));
        });
        Assert.True(gameThreadBlocked.Wait(TimeSpan.FromSeconds(10)), "failed to block the game-loop thread");

        try
        {
            handler.Handle_UpdateAiBehaviorInteractablePoint(new MessagePayload<UpdateAiBehaviorInteractablePoint>(
                this,
                new UpdateAiBehaviorInteractablePoint("party-ai-1", "interactable-1")));

            Assert.True(objectManager.AddExisting("party-ai-1", partyAi));
            Assert.True(objectManager.AddExisting("interactable-1", interactablePoint));
        }
        finally
        {
            releaseGameThread.Set();
        }

        GameThread.Run(() => { }, blocking: true);

        Assert.Same(interactablePoint, partyAi.AiBehaviorInteractable);
    }
}
