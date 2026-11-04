using Autofac;
using Common;
using Common.Logging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Moq;
using System;
using System.Collections.Concurrent;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

[Collection(ModInformationRoleCollection.Name)]
public class PartyRemovalDiagnosticsPatchesTests
{
    private const string PartyId = "Created_186155";

    [Fact]
    public void ClientRemovalOfRegisteredPartyOutsideTheReceivePath_Warns()
    {
        var messages = RemoveParty(isServer: false, isRegistered: true, scopeName: "none", out _);

        Assert.Contains(PartyId, Assert.Single(messages, IsRemovalWarning));
    }

    [Fact]
    public void ClientRemovalOfUnregisteredParty_DoesNotWarn()
    {
        var messages = RemoveParty(isServer: false, isRegistered: false, scopeName: "none", out _);

        Assert.DoesNotContain(messages, IsRemovalWarning);
    }

    [Theory]
    [InlineData("allowed-thread")]
    [InlineData("save-load")]
    [InlineData("local-operation")]
    public void ClientRemovalWhileOriginalsAreAllowed_DoesNotWarnOrLookUpTheParty(string scopeName)
    {
        var messages = RemoveParty(isServer: false, isRegistered: true, scopeName, out var objectManager);

        Assert.DoesNotContain(messages, IsRemovalWarning);
        string id;
        objectManager.Verify(m => m.TryGetId(It.IsAny<object>(), out id), Times.Never);
    }

    [Fact]
    public void ServerRemoval_DoesNotWarnOrLookUpTheParty()
    {
        var messages = RemoveParty(isServer: true, isRegistered: true, scopeName: "none", out var objectManager);

        Assert.DoesNotContain(messages, IsRemovalWarning);
        string id;
        objectManager.Verify(m => m.TryGetId(It.IsAny<object>(), out id), Times.Never);
    }

    [Theory]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    public void ShouldWarn_OnlyForAClientRemovingARegisteredPartyOutsideAllowedOriginals(
        bool isServer, bool isRegistered, bool originalsAllowed, bool expected)
    {
        Assert.Equal(expected, PartyRemovalDiagnosticsPatches.ShouldWarn(isServer, isRegistered, originalsAllowed));
    }

    private static bool IsRemovalWarning(string message) => message.Contains("without a server destroy message");

    private static ConcurrentQueue<string> RemoveParty(bool isServer, bool isRegistered, string scopeName,
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
            ModInformation.IsServer = isServer;
            using (ContainerProvider.UseContainerThreadSafe(container))
            using (IDisposable? scope = scopeName switch
            {
                "allowed-thread" => new AllowedThread(),
                "save-load" => CallOriginalPolicy.AllowOriginalsOnAllThreads(),
                "local-operation" => CallOriginalPolicy.AllowOriginalsForCurrentOperation(),
                _ => null,
            })
            {
                PartyRemovalDiagnosticsPatches.RemovePartyPrefix(party);
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
