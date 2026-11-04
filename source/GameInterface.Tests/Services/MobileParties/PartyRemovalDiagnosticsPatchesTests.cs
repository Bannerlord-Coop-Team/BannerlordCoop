using Common;
using Common.Logging;
using Common.Util;
using GameInterface.Services.MobileParties.Patches;
using System;
using System.Collections.Concurrent;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

[Collection(ModInformationRoleCollection.Name)]
public class PartyRemovalDiagnosticsPatchesTests
{
    [Fact]
    public void ClientRemovalOutsideTheReceivePath_Warns()
    {
        var messages = CaptureLogs(() =>
            Assert.True(PartyRemovalDiagnosticsPatches.WarnIfClientLocalRemoval("Created_186155")), isServer: false);

        Assert.Contains(messages, message =>
            message.Contains("Created_186155") && message.Contains("without a server destroy message"));
    }

    [Fact]
    public void ClientRemovalWhileApplyingAServerMessage_DoesNotWarn()
    {
        var messages = CaptureLogs(() =>
        {
            using (new AllowedThread())
                Assert.False(PartyRemovalDiagnosticsPatches.WarnIfClientLocalRemoval("Created_186155"));
        }, isServer: false);

        Assert.DoesNotContain(messages, message => message.Contains("Created_186155"));
    }

    [Fact]
    public void ServerRemoval_DoesNotWarn()
    {
        var messages = CaptureLogs(() =>
            Assert.False(PartyRemovalDiagnosticsPatches.WarnIfClientLocalRemoval("Created_186155")), isServer: true);

        Assert.DoesNotContain(messages, message => message.Contains("Created_186155"));
    }

    private static ConcurrentQueue<string> CaptureLogs(Action action, bool isServer)
    {
        var messages = new ConcurrentQueue<string>();
        Action<string> callback = messages.Enqueue;
        bool originalIsServer = ModInformation.IsServer;
        OutputSinkManager.AddLogCallback(callback);
        try
        {
            ModInformation.IsServer = isServer;
            action();
        }
        finally
        {
            ModInformation.IsServer = originalIsServer;
            OutputSinkManager.RemoveLogCallback(callback);
        }
        return messages;
    }
}
