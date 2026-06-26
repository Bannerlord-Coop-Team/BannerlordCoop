using Autofac;
using Common;
using Common.Messaging;
using Common.Network;
using Common.Tests.Utils;
using Common.Util;
using Coop.Tests.Mocks;
using GameInterface.Services.GameDebug.Commands;
using GameInterface.Services.GameDebug.Metrics;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Moq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.GameDebug.Metrics;

[CollectionDefinition(nameof(PartySyncPerformanceLogsCommandCollection), DisableParallelization = true)]
public class PartySyncPerformanceLogsCommandCollection
{
}

[Collection(nameof(PartySyncPerformanceLogsCommandCollection))]
public class PartySyncPerformanceLogsCommandTests : IDisposable
{
    private readonly IContainer container;
    private readonly Mock<IPartySyncPerformanceLogger> logger = new();

    public PartySyncPerformanceLogsCommandTests()
    {
        ModInformation.IsServer = false;

        var builder = new ContainerBuilder();
        builder.RegisterInstance(logger.Object).As<IPartySyncPerformanceLogger>();
        container = builder.Build();
        ContainerProvider.SetContainer(container);
    }

    public void Dispose()
    {
        ContainerProvider.Clear();
        container.Dispose();
        ModInformation.IsServer = false;
    }

    [Fact]
    public void On_ValidArgs_EnablesLogger()
    {
        logger.Setup(l => l.Enable(TimeSpan.FromSeconds(60), "test_log")).Returns("enabled");

        var result = PartySyncPerformanceLogsCommand.PartySyncPerformanceLogs(new List<string> { "on", "60", "test_log" });

        Assert.Equal("enabled", result);
        logger.Verify(l => l.Enable(TimeSpan.FromSeconds(60), "test_log"), Times.Once);
    }

    [Fact]
    public void Off_DisablesLogger()
    {
        logger.Setup(l => l.Disable()).Returns("disabled");

        var result = PartySyncPerformanceLogsCommand.PartySyncPerformanceLogs(new List<string> { "off" });

        Assert.Equal("disabled", result);
        logger.Verify(l => l.Disable(), Times.Once);
    }

    [Fact]
    public void Status_ReturnsLoggerStatus()
    {
        logger.Setup(l => l.Status()).Returns("status");

        var result = PartySyncPerformanceLogsCommand.PartySyncPerformanceLogs(new List<string> { "status" });

        Assert.Equal("status", result);
        logger.Verify(l => l.Status(), Times.Once);
    }

    [Fact]
    public void On_MissingArgs_ReturnsUsage()
    {
        var result = PartySyncPerformanceLogsCommand.PartySyncPerformanceLogs(new List<string> { "on", "60" });

        Assert.Contains("Usage:", result);
        logger.Verify(l => l.Enable(It.IsAny<TimeSpan>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void On_NonPositiveSeconds_ReturnsError()
    {
        var result = PartySyncPerformanceLogsCommand.PartySyncPerformanceLogs(new List<string> { "on", "0", "test_log" });

        Assert.Equal("Seconds must be a positive number", result);
        logger.Verify(l => l.Enable(It.IsAny<TimeSpan>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void On_FileWithoutCsv_WritesCsvFileName()
    {
        var fileWriter = new FakeFileWriter();
        using var realContainer = CreateLoggerContainer(fileWriter);
        ContainerProvider.SetContainer(realContainer);

        var result = PartySyncPerformanceLogsCommand.PartySyncPerformanceLogs(new List<string> { "on", "60", "test_log" });

        Assert.Contains("test_log.csv", result);
        var write = Assert.Single(fileWriter.Writes);
        Assert.Equal("test_log.csv", write.Path);
    }

    [Fact]
    public void On_InvalidFilename_ReturnsValidationError()
    {
        var fileWriter = new FakeFileWriter();
        using var realContainer = CreateLoggerContainer(fileWriter);
        ContainerProvider.SetContainer(realContainer);

        var result = PartySyncPerformanceLogsCommand.PartySyncPerformanceLogs(new List<string> { "on", "60", "nested\\test_log" });

        Assert.Equal("Filename must not include a path", result);
        Assert.Empty(fileWriter.Writes);
    }

    [Fact]
    public void Command_WhenServer_ReturnsClientOnlyError()
    {
        ModInformation.IsServer = true;

        var result = PartySyncPerformanceLogsCommand.PartySyncPerformanceLogs(new List<string> { "status" });

        Assert.Equal("party_sync_performance_logs can only be called by a client", result);
        logger.Verify(l => l.Status(), Times.Never);
    }

    private static IContainer CreateLoggerContainer(FakeFileWriter fileWriter)
    {
        var configuration = new Mock<INetworkConfig>();
        configuration.SetupGet(c => c.AuditTimeout).Returns(TimeSpan.FromSeconds(15));

        var builder = new ContainerBuilder();
        builder.RegisterInstance(new TestMessageBroker()).As<IMessageBroker>();
        builder.RegisterInstance(new TestNetwork()).As<INetwork>();
        builder.RegisterInstance(configuration.Object).As<INetworkConfig>();
        builder.RegisterInstance(new FakePartyProvider()).As<IPartySyncPerformancePartyProvider>();
        builder.RegisterInstance(new FakeClock()).As<IPartySyncPerformanceClock>();
        builder.RegisterInstance(fileWriter).As<IPartySyncPerformanceFileWriter>();
        builder.RegisterType<PartySyncPerformanceLogger>().As<IPartySyncPerformanceLogger>().InstancePerLifetimeScope();
        return builder.Build();
    }
}

[Collection(nameof(PartySyncPerformanceLogsCommandCollection))]
public class PartySyncPerformanceSerializationTests
{
    [Fact]
    public void RequestMessage_ProtoSerializes()
    {
        var original = new NetworkRequestPartySyncPerformanceSnapshot(12);

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        var copy = Serializer.Deserialize<NetworkRequestPartySyncPerformanceSnapshot>(stream);

        Assert.Equal(12, copy.RequestId);
    }

    [Fact]
    public void SnapshotMessage_ProtoSerializes()
    {
        var original = new NetworkPartySyncPerformanceSnapshot(34, new[]
        {
            Data("party-a", "Party A", 1f, 2f),
        });

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        var copy = Serializer.Deserialize<NetworkPartySyncPerformanceSnapshot>(stream);

        Assert.Equal(34, copy.RequestId);
        var data = Assert.Single(copy.Data);
        Assert.Equal("party-a", data.MobilePartyId);
        Assert.Equal("Party A", data.Name);
        Assert.Equal(1f, data.Position.X);
        Assert.Equal(2f, data.Position.Y);
    }

    private static PartySyncPerformanceData Data(string id, string name, float x, float y) =>
        new(id, name, new CampaignVec2(new Vec2(x, y), true));
}

[Collection(nameof(PartySyncPerformanceLogsCommandCollection))]
public class PartySyncPerformanceLoggerTests : IDisposable
{
    private readonly TestMessageBroker messageBroker = new();
    private readonly TestNetwork network = new();
    private readonly FakePartyProvider partyProvider = new();
    private readonly FakeClock clock = new();
    private readonly FakeFileWriter fileWriter = new();
    private readonly Mock<INetworkConfig> configuration = new();
    private readonly PartySyncPerformanceLogger logger;
    private readonly NetPeer peer;

    public PartySyncPerformanceLoggerTests()
    {
        ModInformation.IsServer = false;
        configuration.SetupGet(c => c.AuditTimeout).Returns(TimeSpan.FromSeconds(15));
        logger = new PartySyncPerformanceLogger(
            messageBroker,
            network,
            configuration.Object,
            partyProvider,
            clock,
            fileWriter);
        peer = network.CreatePeer();
    }

    public void Dispose()
    {
        logger.Dispose();
        ModInformation.IsServer = false;
    }

    [Fact]
    public void Enable_OverwritesFileAndAppendsCsvSuffix()
    {
        var result = logger.Enable(TimeSpan.FromSeconds(60), "test_log");

        Assert.Contains("Party sync performance logs are ON", result);
        var write = Assert.Single(fileWriter.Writes);
        Assert.Equal("test_log.csv", write.Path);
        Assert.StartsWith("timestamp_utc,interval_seconds", write.Contents);
    }

    [Fact]
    public void Enable_InvalidFilename_ReturnsError()
    {
        var result = logger.Enable(TimeSpan.FromSeconds(60), "nested\\test_log");

        Assert.Equal("Filename must not include a path", result);
        Assert.Empty(fileWriter.Writes);
    }

    [Fact]
    public void RequestSnapshot_SendsRequestAndSuppressesPendingDuplicate()
    {
        logger.Enable(TimeSpan.FromSeconds(60), "test_log");

        logger.RequestSnapshot();
        logger.RequestSnapshot();

        var requests = network.GetPeerMessagesFromType<NetworkRequestPartySyncPerformanceSnapshot>(peer).ToList();
        Assert.Single(requests);
        Assert.Equal(1, requests[0].RequestId);
        Assert.Contains("Skipped pending requests: 1", logger.Status());
    }

    [Fact]
    public void RequestSnapshot_TimedOutPendingRequestSendsReplacement()
    {
        logger.Enable(TimeSpan.FromSeconds(60), "test_log");

        logger.RequestSnapshot();
        clock.UtcNow = clock.UtcNow.AddSeconds(16);
        logger.RequestSnapshot();

        var requests = network.GetPeerMessagesFromType<NetworkRequestPartySyncPerformanceSnapshot>(peer).ToList();
        Assert.Equal(2, requests.Count);
        Assert.Equal(2, requests[1].RequestId);
    }

    [Fact]
    public void HandleSnapshot_WritesMatchedDistanceAggregate()
    {
        logger.Enable(TimeSpan.FromSeconds(60), "test_log");
        partyProvider.Data = new[]
        {
            Data("party-a", "Client A", 3f, 4f),
            Data("party-c", "Client C", 10f, 10f),
        };

        logger.RequestSnapshot();
        logger.HandleSnapshotOnGameThread(new NetworkPartySyncPerformanceSnapshot(1, new[]
        {
            Data("party-a", "Server A", 0f, 0f),
            Data("party-b", "Server B", 1f, 1f),
        }));

        var append = Assert.Single(fileWriter.Appends);
        Assert.Equal("test_log.csv", append.Path);

        var columns = append.Contents.Trim().Split(',');
        Assert.Equal("2026-06-26T12:00:00.0000000Z", columns[0]);
        Assert.Equal("60", columns[1]);
        Assert.Equal("2", columns[2]);
        Assert.Equal("2", columns[3]);
        Assert.Equal("1", columns[4]);
        Assert.Equal("1", columns[5]);
        Assert.Equal("1", columns[6]);
        Assert.Equal("5", columns[7]);
        Assert.Equal("5", columns[8]);
        Assert.Equal("5", columns[9]);
    }

    [Fact]
    public void Disable_StopsFurtherRequests()
    {
        logger.Enable(TimeSpan.FromSeconds(60), "test_log");

        logger.Disable();
        logger.RequestSnapshot();

        Assert.False(network.SentNetworkMessages.ContainsKey(peer.Id));
    }

    [Fact]
    public void GameLoadStarted_ResetsLogger()
    {
        logger.Enable(TimeSpan.FromSeconds(60), "test_log");

        messageBroker.Publish(this, new GameLoadStarted());
        logger.RequestSnapshot();

        Assert.Equal("Party sync performance logs are OFF", logger.Status());
        Assert.False(network.SentNetworkMessages.ContainsKey(peer.Id));
    }

    private static PartySyncPerformanceData Data(string id, string name, float x, float y) =>
        new(id, name, new CampaignVec2(new Vec2(x, y), true));
}

[Collection(nameof(PartySyncPerformanceLogsCommandCollection))]
public class PartySyncPerformanceHandlerTests : IDisposable
{
    private readonly TestMessageBroker messageBroker = new();
    private readonly TestNetwork network = new();
    private readonly FakePartyProvider partyProvider = new();
    private readonly Mock<IPartySyncPerformanceLogger> logger = new();
    private readonly PartySyncPerformanceHandler handler;

    public PartySyncPerformanceHandlerTests()
    {
        handler = new PartySyncPerformanceHandler(messageBroker, network, partyProvider, logger.Object);
    }

    public void Dispose()
    {
        handler.Dispose();
        ModInformation.IsServer = false;
    }

    [Fact]
    public void Request_OnServer_SendsSnapshotToRequestingPeer()
    {
        ModInformation.IsServer = true;
        var peer = network.CreatePeer();
        partyProvider.Data = new[] { Data("party-a", "Party A", 1f, 2f) };

        handler.Handle_Request(new MessagePayload<NetworkRequestPartySyncPerformanceSnapshot>(
            peer,
            new NetworkRequestPartySyncPerformanceSnapshot(42)));

        var response = Assert.Single(network.GetPeerMessagesFromType<NetworkPartySyncPerformanceSnapshot>(peer));
        Assert.Equal(42, response.RequestId);
        Assert.Single(response.Data);
        Assert.Equal("party-a", response.Data[0].MobilePartyId);
    }

    [Fact]
    public void Response_OnClient_DelegatesToLogger()
    {
        ModInformation.IsServer = false;
        var snapshot = new NetworkPartySyncPerformanceSnapshot(7, Array.Empty<PartySyncPerformanceData>());

        handler.Handle_Response(new MessagePayload<NetworkPartySyncPerformanceSnapshot>(this, snapshot));

        logger.Verify(l => l.HandleSnapshot(snapshot), Times.Once);
    }

    [Fact]
    public void PartyProvider_UnresolvedPartyId_SkipsParty()
    {
        var objectManager = new Mock<IObjectManager>();
        var provider = new PartySyncPerformancePartyProvider(objectManager.Object);
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        string unused = string.Empty;
        objectManager.Setup(o => o.TryGetId(party, out unused)).Returns(false);

        var result = provider.GetPartyData(new[] { party });

        Assert.Empty(result);
    }

    private static PartySyncPerformanceData Data(string id, string name, float x, float y) =>
        new(id, name, new CampaignVec2(new Vec2(x, y), true));
}

internal class FakePartyProvider : IPartySyncPerformancePartyProvider
{
    public PartySyncPerformanceData[] Data { get; set; } = Array.Empty<PartySyncPerformanceData>();

    public PartySyncPerformanceData[] GetPartyData() => Data;
}

internal class FakeClock : IPartySyncPerformanceClock
{
    public DateTime UtcNow { get; set; } = new(2026, 6, 26, 12, 0, 0, DateTimeKind.Utc);
}

internal class FakeFileWriter : IPartySyncPerformanceFileWriter
{
    public List<(string Path, string Contents)> Writes { get; } = new();
    public List<(string Path, string Contents)> Appends { get; } = new();

    public void WriteAllText(string path, string contents)
    {
        Writes.Add((path, contents));
    }

    public void AppendAllText(string path, string contents)
    {
        Appends.Add((path, contents));
    }
}
