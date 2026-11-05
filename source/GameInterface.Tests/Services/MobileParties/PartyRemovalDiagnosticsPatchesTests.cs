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
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

[Collection(ModInformationRoleCollection.Name)]
public class PartyRemovalDiagnosticsPatchesTests
{
    private const string PartyId = "Created_186155";

    [Fact]
    public void ClientRemovalOutsideTheReceivePath_LogsOneErrorWithThePartyIdAndCaller()
    {
        var messages = RemoveParty(isRegistered: true, scopeName: "none", out _);

        string error = Assert.Single(messages, IsClientRemovalError);
        Assert.Contains(PartyId, error);
        Assert.Contains(nameof(PartyRemovalDiagnosticsPatchesTests), error);
    }

    [Fact]
    public void ClientDestroyOfOtherManagedType_StillLogsTheGenericError()
    {
        var equipment = new Equipment();
        var messages = new ConcurrentQueue<string>();
        Action<string> callback = messages.Enqueue;
        bool originalIsServer = ModInformation.IsServer;
        bool hadContainer = ContainerProvider.TryGetContainer(out var previousContainer);
        var builder = new ContainerBuilder();
        builder.RegisterInstance(new TestSyncPolicy()).As<ISyncPolicy>();
        using var container = builder.Build();
        OutputSinkManager.AddLogCallback(callback);
        try
        {
            ModInformation.IsServer = false;
            using (ContainerProvider.UseContainerThreadSafe(container))
            {
                LifetimePatches<Equipment>.DestroyPostfix(ref equipment, true);
            }
        }
        finally
        {
            ModInformation.IsServer = originalIsServer;
            OutputSinkManager.RemoveLogCallback(callback);
            if (hadContainer) ContainerProvider.SetContainer(previousContainer);
            else ContainerProvider.Clear();
        }

        Assert.Single(messages, m => m.Contains("Client destroyed managed"));
    }

    [Fact]
    public void ClientRemovalOfUnregisteredParty_SaysItIsUnregistered()
    {
        var messages = RemoveParty(isRegistered: false, scopeName: "none", out _);

        Assert.Contains("(unregistered)", Assert.Single(messages, IsClientRemovalError));
    }

    [Theory]
    [InlineData("allowed-thread")]
    [InlineData("save-load")]
    [InlineData("local-operation")]
    public void ClientRemovalWhileOriginalsAreAllowed_DoesNotLogOrLookUpTheParty(string scopeName)
    {
        var messages = RemoveParty(isRegistered: true, scopeName, out var objectManager);

        Assert.DoesNotContain(messages, IsClientRemovalError);
        string id;
        objectManager.Verify(m => m.TryGetId(It.IsAny<object>(), out id), Times.Never);
    }

    private static bool IsClientRemovalError(string message) => message.Contains("Client destroyed");

    private static ConcurrentQueue<string> RemoveParty(bool isRegistered, string scopeName,
        out Mock<IObjectManager> objectManager)
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        objectManager = new Mock<IObjectManager>();
        string id = PartyId;
        objectManager.Setup(m => m.TryGetId(party, out id)).Returns(isRegistered);

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
            using (IDisposable? scope = scopeName switch
            {
                "allowed-thread" => new AllowedThread(),
                "save-load" => CallOriginalPolicy.AllowOriginalsOnAllThreads(),
                "local-operation" => CallOriginalPolicy.AllowOriginalsForCurrentOperation(),
                _ => null,
            })
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

    /// <summary>Gameplay sync policy, so only the scopes under test allow originals.</summary>
    private sealed class TestSyncPolicy : ISyncPolicy
    {
        public bool AllowOriginal() => false;
    }
}
