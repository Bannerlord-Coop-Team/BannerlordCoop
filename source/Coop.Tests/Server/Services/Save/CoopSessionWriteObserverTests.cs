#if DEBUG
using Common;
using Common.Commands;
using Common.Network;
using Common.Tests.Utils;
using Coop.Core.Server.Services.Save;
using Coop.Core.Server.Services.Save.Commands;
using Coop.Core.Server.Services.Save.Handlers;
using Coop.Core.Server.Services.Save.Messages;
using GameInterface.CoopSessionData;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Moq;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Coop.Tests.Server.Services.Save;

[Collection(ModInformationRoleCollection.Name)]
public class CoopSessionWriteObserverTests
{
    private const string SaveName = "LiveTestWorld";
    private const string Attempt = "run-7.save-1";

    private readonly TestMessageBroker broker = new TestMessageBroker();
    private DateTime now = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    private CoopSessionWriteObserver CreateObserver() => new CoopSessionWriteObserver(broker, () => now = now.AddSeconds(1));

    private void Write(string saveName, bool success) => broker.Publish(new object(), new CoopSessionWritten(saveName, success));

    [Theory]
    [InlineData(true, "succeeded")]
    [InlineData(false, "failed")]
    public void ArmedSave_RecordsTheTypedOutcome(bool success, string expected)
    {
        using var observer = CreateObserver();
        Assert.Equal("armed", observer.Arm(Attempt, SaveName).Status);

        Write(SaveName, success);
        var read = observer.Read(Attempt);

        Assert.Equal("observed", read.Status);
        Assert.Equal(expected, read.Result);
        var outcome = Assert.Single(read.Outcomes);
        Assert.Equal(success, outcome.Success);
        Assert.True(outcome.Utc > read.ArmedUtc);
    }

    [Fact]
    public void EventsBeforeArm_AreCountedButNeverRecorded()
    {
        using var observer = CreateObserver();
        Write(SaveName, true);

        observer.Arm(Attempt, SaveName);
        var read = observer.Read(Attempt);

        Assert.Equal("pending", read.Result);
        Assert.Empty(read.Outcomes);
        Assert.Equal(1, read.UnarmedCount);
    }

    [Fact]
    public void TransferAndOtherSaves_AreIgnored()
    {
        using var observer = CreateObserver();
        observer.Arm(Attempt, SaveName);

        Write("TransferSave", true);
        Write("OtherWorld", false);
        var read = observer.Read(Attempt);

        Assert.Equal("pending", read.Result);
        Assert.Equal(2, read.IgnoredCount);
        Assert.Equal(new[] { "TransferSave", "OtherWorld" }, read.IgnoredSaveNames);
    }

    [Fact]
    public void RepeatedOutcomes_AreAmbiguousNotSuccess()
    {
        using var observer = CreateObserver();
        observer.Arm(Attempt, SaveName);

        Write(SaveName, true);
        Write(SaveName, true);

        var read = observer.Read(Attempt);
        Assert.Equal("ambiguous", read.Result);
        Assert.Equal(new long[] { 1, 2 }, read.Outcomes.Select(outcome => outcome.Sequence));
    }

    [Fact]
    public void Rearm_NeverDiscardsAPendingOrUnreadObservation()
    {
        using var observer = CreateObserver();
        observer.Arm(Attempt, SaveName);

        Assert.Equal("refused", observer.Arm("run-7.save-2", SaveName).Status);
        Write(SaveName, true);
        Assert.Equal("refused", observer.Arm("run-7.save-2", SaveName).Status);

        var read = observer.Read(Attempt);
        Assert.Equal("succeeded", read.Result);
        Assert.Equal(Attempt, read.AttemptId);
    }

    [Fact]
    public void WrongOrStaleAttempt_IsRefused()
    {
        using var observer = CreateObserver();
        observer.Arm(Attempt, SaveName);
        Write(SaveName, true);

        Assert.Equal("refused", observer.Read("run-6.save-1").Status);
        Assert.Equal("refused", observer.Release("run-6.save-1").Status);

        observer.Read(Attempt);
        observer.Release(Attempt);
        Assert.Equal("refused", observer.Read(Attempt).Status);
    }

    [Fact]
    public void Release_RequiresTheCurrentStateToHaveBeenRead()
    {
        using var observer = CreateObserver();
        observer.Arm(Attempt, SaveName);

        Assert.Equal("refused", observer.Release(Attempt).Status);
        observer.Read(Attempt);
        Write(SaveName, true);
        Assert.Equal("refused", observer.Release(Attempt).Status);

        Assert.Equal("succeeded", observer.Read(Attempt).Result);
        var released = observer.Release(Attempt);
        Assert.Equal("released", released.Status);
        Assert.Equal("succeeded", released.Result);
        Assert.NotNull(released.ReleasedUtc);
        Assert.Equal("armed", observer.Arm("run-7.save-2", SaveName).Status);
    }

    [Theory]
    [InlineData("", SaveName)]
    [InlineData("has space", SaveName)]
    [InlineData(Attempt, "")]
    [InlineData(Attempt, "bad/name")]
    public void InvalidAttemptOrSaveName_IsRefused(string attempt, string saveName)
    {
        using var observer = CreateObserver();

        Assert.Equal("refused", observer.Arm(attempt, saveName).Status);
        Assert.Equal("armed", observer.Arm(Attempt, SaveName).Status);
    }

    [Fact]
    public void Dispose_Unsubscribes()
    {
        var observer = CreateObserver();
        observer.Arm(Attempt, SaveName);

        observer.Dispose();
        Write(SaveName, true);

        Assert.True(broker.GetSubscriberCountForType<CoopSessionWritten>() <= 0);
        Assert.Equal("refused", observer.Read(Attempt).Status);
    }

    [Theory]
    [InlineData(false, "succeeded")]
    [InlineData(true, "failed")]
    public void ProductionSessionWrite_IsObservedThroughSaveGameHandler(bool writeThrows, string expected)
    {
        using var observer = CreateObserver();
        using var handler = CreateSavingHandler(writeThrows);
        observer.Arm(Attempt, SaveName);

        broker.Publish(new object(), new GameSaved(SaveName));

        Assert.Equal(expected, observer.Read(Attempt).Result);
    }

    [Fact]
    public void Commands_ReportExplicitStatusInTheirOutput()
    {
        using var observer = CreateObserver();
        var registry = CreateRegistry(observer);

        var armed = RunAs(isServer: true, () => registry.ProcessCommand("coop.debug.save.session_arm", Args(Attempt, SaveName)));
        Write(SaveName, true);
        var read = RunAs(isServer: true, () => registry.ProcessCommand("coop.debug.save.session_result", Args(Attempt)));
        var released = RunAs(isServer: true, () => registry.ProcessCommand("coop.debug.save.session_release", Args(Attempt)));

        Assert.Equal("armed", (string)JObject.Parse(armed.Output)["status"]!);
        var output = JObject.Parse(read.Output);
        Assert.Equal("coop-session-written-observer", (string)output["proof"]!);
        Assert.Equal("observed", (string)output["status"]!);
        Assert.Equal("succeeded", (string)output["result"]!);
        Assert.Equal(SaveName, (string)output["saveName"]!);
        Assert.True((bool)output["outcomes"]![0]!["success"]!);
        Assert.Equal("released", (string)JObject.Parse(released.Output)["status"]!);
    }

    [Fact]
    public void RefusedCommand_StillCarriesItsStatusInTheOutput()
    {
        using var observer = CreateObserver();
        var registry = CreateRegistry(observer);

        var result = RunAs(isServer: true, () => registry.ProcessCommand("coop.debug.save.session_result", Args(Attempt)));

        // The live-test transport forwards only the output, so the refusal must be readable from it.
        Assert.False(result.Succeeded);
        Assert.Equal("observation_refused", result.ErrorCode);
        Assert.Equal("refused", (string)JObject.Parse(result.Output)["status"]!);
    }

    [Fact]
    public void Commands_AreServerOnly()
    {
        using var observer = CreateObserver();
        var registry = CreateRegistry(observer);

        var viaRegistry = RunAs(isServer: false, () => registry.ProcessCommand("coop.debug.save.session_arm", Args(Attempt, SaveName)));
        var direct = RunAs(isServer: false, () => new SessionWriteDebugCommands.SessionArmCoopCommand(observer).ProcessCommand(Args(Attempt, SaveName)));

        Assert.Equal("command_wrong_side", viaRegistry.ErrorCode);
        Assert.Equal("refused", (string)JObject.Parse(direct.Output)["status"]!);
        Assert.Equal("armed", observer.Arm(Attempt, SaveName).Status);
        Assert.All(registry.Commands, descriptor => Assert.Equal(CoopCommandSide.Server, descriptor.Side));
    }

    private static CoopCommandRegistry CreateRegistry(ICoopSessionWriteObserver observer) =>
        new CoopCommandRegistry(new ICoopCommand[]
        {
            new SessionWriteDebugCommands.SessionArmCoopCommand(observer),
            new SessionWriteDebugCommands.SessionResultCoopCommand(observer),
            new SessionWriteDebugCommands.SessionReleaseCoopCommand(observer),
        }, new LoggerConfiguration().CreateLogger());

    private SaveGameHandler CreateSavingHandler(bool writeThrows)
    {
        var registry = new Mock<IPlayerManager>();
        registry.SetupGet(value => value.Players).Returns(Array.Empty<Player>());
        return new SaveGameHandler(
            broker,
            new StubSaveManager(writeThrows ? new IOException("disk full") : null),
            new Mock<ICoopSessionProvider>().Object,
            registry.Object,
            new Mock<IPlayerPartyRestorer>().Object,
            new Mock<INetwork>().Object,
            new Mock<IObjectManager>().Object);
    }

    private static ICoopCommandArgs Args(params string[] values) => new CoopCommandArgsFactory().FromValues(values);

    private static CoopCommandResult RunAs(bool isServer, Func<CoopCommandResult> run)
    {
        bool wasServer = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = isServer;
            return run();
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    private sealed class StubSaveManager : ICoopSaveManager
    {
        private readonly Exception? writeFailure;

        public StubSaveManager(Exception? writeFailure)
        {
            this.writeFailure = writeFailure;
        }

        public string DefaultPath => string.Empty;
        public string FileType => ".json";

        public ICoopSession LoadCoopSession(string saveName) => null!;

        public void SaveCoopSession(string saveName, ICoopSession session)
        {
            if (writeFailure != null) throw writeFailure;
        }
    }
}
#endif
