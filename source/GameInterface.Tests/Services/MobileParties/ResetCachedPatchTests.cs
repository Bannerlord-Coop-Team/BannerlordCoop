using Autofac;
using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Patches;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

/// <summary>
/// Checks that local cache initialization does not become a replicated party change.
/// </summary>
[Collection(ModInformationRoleCollection.Name)]
public class ResetCachedPatchTests
{
    [Theory]
    [InlineData(true, false, "none", 1)]
    [InlineData(false, false, "none", 0)]
    [InlineData(true, true, "none", 0)]
    [InlineData(true, false, "allowed-thread", 0)]
    [InlineData(true, false, "save-load", 0)]
    [InlineData(true, false, "local-operation", 0)]
    public void Reset_PublishesOnlyDuringAuthoritativeGameplay(
        bool isServer, bool allowOriginal, string scopeName, int expectedCount)
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(new TestSyncPolicy(allowOriginal)).As<ISyncPolicy>();
        using var container = builder.Build();
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var messages = new List<ResetMobilePartyCached>();
        void Capture(MessagePayload<ResetMobilePartyCached> payload) => messages.Add(payload.What);

        bool wasServer = ModInformation.IsServer;
        bool hadContainer = ContainerProvider.TryGetContainer(out var previousContainer);
        MessageBroker.Instance.Subscribe<ResetMobilePartyCached>(Capture);
        try
        {
            ModInformation.IsServer = isServer;
            using (ContainerProvider.UseContainerThreadSafe(container))
            {
                using (IDisposable? scope = scopeName switch
                {
                    "allowed-thread" => new AllowedThread(),
                    "save-load" => CallOriginalPolicy.AllowOriginalsOnAllThreads(),
                    "local-operation" => CallOriginalPolicy.AllowOriginalsForCurrentOperation(),
                    _ => null,
                })
                {
                    ResetCachedPatch.ResetCachedPostfix(party);
                }

                Assert.Equal(expectedCount, messages.Count);
                Assert.All(messages, message => Assert.Same(party, message.MobileParty));

                // Leaving a local-only scope must not suppress later gameplay resets.
                if (scopeName != "none")
                {
                    ResetCachedPatch.ResetCachedPostfix(party);
                    Assert.Same(party, Assert.Single(messages).MobileParty);
                }
            }
        }
        finally
        {
            MessageBroker.Instance.Unsubscribe<ResetMobilePartyCached>(Capture);
            ModInformation.IsServer = wasServer;
            if (hadContainer) ContainerProvider.SetContainer(previousContainer);
            else ContainerProvider.Clear();
        }
    }

    /// <summary>Supplies the lifecycle policy without starting a campaign.</summary>
    private sealed class TestSyncPolicy : ISyncPolicy
    {
        private readonly bool allowOriginal;
        public TestSyncPolicy(bool allowOriginal) => this.allowOriginal = allowOriginal;
        public bool AllowOriginal() => allowOriginal;
    }
}
