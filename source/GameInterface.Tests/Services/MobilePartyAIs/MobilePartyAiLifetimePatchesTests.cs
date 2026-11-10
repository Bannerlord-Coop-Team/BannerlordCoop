using Autofac;
using Common;
using Common.Logging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Registry.Auto;
using GameInterface.Services.MobilePartyAIs.Patches;
using GameInterface.Services.ObjectManager;
using Moq;
using System;
using System.Collections.Concurrent;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobilePartyAIs;

[Collection(ModInformationRoleCollection.Name)]
public class MobilePartyAiLifetimePatchesTests
{
    private const string PartyId = "Created_186155";

    [Fact]
    public void ClientRemovalOutsideTheReceivePath_LogsOneErrorWithThePartyIdAndCaller()
    {
        var messages = RemoveParty();

        string error = Assert.Single(messages, message => message.Contains("Client destroyed"));
        Assert.Contains(PartyId, error);
        Assert.Contains(nameof(MobilePartyAiLifetimePatchesTests), error);
    }

    private static ConcurrentQueue<string> RemoveParty()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var objectManager = new Mock<IObjectManager>();
        string id = PartyId;
        objectManager.Setup(m => m.TryGetId(party, out id)).Returns(true);

        var builder = new ContainerBuilder();
        builder.RegisterInstance(objectManager.Object).As<IObjectManager>();
        builder.RegisterInstance(new TestSyncPolicy()).As<ISyncPolicy>();
        using var container = builder.Build();

        var messages = new ConcurrentQueue<string>();
        Action<string> callback = messages.Enqueue;
        bool originalIsServer = ModInformation.IsServer;
        bool hadContainer = ContainerProvider.TryGetContainer(out var previousContainer);
        OutputSinkManager.AddLogCallback(callback);
        try
        {
            ModInformation.IsServer = false;
            using (ContainerProvider.UseContainerThreadSafe(container))
            {
                // RemoveParty carries both postfixes, the auto registry destroy one and the rich one
                LifetimePatches<MobileParty>.DestroyPostfix(ref party, true);
                MobilePartyAiLifetimePatches.RemoveParty_Postfix(ref party);
            }
        }
        finally
        {
            ModInformation.IsServer = originalIsServer;
            OutputSinkManager.RemoveLogCallback(callback);
            if (hadContainer) ContainerProvider.SetContainer(previousContainer);
            else ContainerProvider.Clear();
        }
        return messages;
    }

    /// <summary>Gameplay sync policy, so the removal runs where originals are not allowed.</summary>
    private sealed class TestSyncPolicy : ISyncPolicy
    {
        public bool AllowOriginal() => false;
    }
}
