using GameInterface.Services.BugReporting.Messages;
using ProtoBuf;

namespace Coop.IntegrationTests.BugReporting;

/// <summary>Tests diagnostic bug-report message serialization.</summary>
public class BugReportNetworkMessageSerializationTest
{
    [Fact]
    public void NetworkRequestBugReport_RoundTrips()
    {
        var copy = RoundTrip(new NetworkRequestBugReport("summary", "description"));

        Assert.Equal("summary", copy.Summary);
        Assert.Equal("description", copy.Description);
    }

    [Fact]
    public void NetworkRequestBugReportLogs_RoundTrips()
    {
        var copy = RoundTrip(new NetworkRequestBugReportLogs("request-id"));

        Assert.Equal("request-id", copy.RequestId);
    }

    [Fact]
    public void NetworkBugReportLogChunk_RoundTrips()
    {
        var original = new NetworkBugReportLogChunk(
            "request-id",
            1,
            3,
            130000,
            250000,
            new byte[] { 1, 2, 3 });

        var copy = RoundTrip(original);

        Assert.Equal("request-id", copy.RequestId);
        Assert.Equal(1, copy.ChunkIndex);
        Assert.Equal(3, copy.ChunkCount);
        Assert.Equal(130000, copy.CompressedLength);
        Assert.Equal(250000, copy.UncompressedLength);
        Assert.Equal(new byte[] { 1, 2, 3 }, copy.Data);
    }

    [Fact]
    public void NetworkBugReportLogUnavailable_RoundTrips()
    {
        var copy = RoundTrip(new NetworkBugReportLogUnavailable(
            "request-id",
            BugReportLogUnavailableReason.ConsentNotGranted));

        Assert.Equal("request-id", copy.RequestId);
        Assert.Equal(BugReportLogUnavailableReason.ConsentNotGranted, copy.Reason);
    }

    [Fact]
    public void NetworkBugReportResult_RoundTrips()
    {
        var copy = RoundTrip(new NetworkBugReportResult("request-id", "packaged"));

        Assert.Equal("request-id", copy.RequestId);
        Assert.Equal("packaged", copy.Message);
    }

    private static T RoundTrip<T>(T original)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, original);
        stream.Position = 0;
        return Serializer.Deserialize<T>(stream);
    }
}
