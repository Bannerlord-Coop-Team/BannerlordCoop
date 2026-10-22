using Common.LiveTesting;
using System.Text.Json;

namespace Common.Tests.LiveTesting;

public class LiveTestProtocolTests
{
    [Fact]
    public void RequestRoundTrip_PreservesArgumentBoundaries()
    {
        LiveTestRequest expected = Request(
            "command",
            "{\"name\":\"coop.debug.test.capture\",\"arguments\":[\"argument with spaces\",\"quoted \\\"value\\\"\"]}");

        string json = LiveTestProtocol.SerializeRequest(expected);

        Assert.True(LiveTestProtocol.TryDeserializeRequest(json, out var actual, out var error));
        Assert.Null(error);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal("argument with spaces", actual.Parameters.GetProperty("arguments")[0].GetString());
        Assert.Equal("quoted \"value\"", actual.Parameters.GetProperty("arguments")[1].GetString());
    }

    [Fact]
    public void ResponseSerialization_EscapesMultilineOutputOntoOnePhysicalLine()
    {
        LiveTestResponse response = LiveTestResponse.Success(
            "request-1",
            Process(),
            new { found = true, output = "first line\r\nsecond line" });

        string json = LiveTestProtocol.SerializeResponse(response);

        Assert.DoesNotContain('\r', json);
        Assert.DoesNotContain('\n', json);
        Assert.Contains("first line\\r\\nsecond line", json);
        Assert.True(LiveTestProtocol.TryDeserializeResponse(json, out var actual, out var error));
        Assert.Null(error);
        JsonElement result = Assert.IsType<JsonElement>(actual.Result);
        Assert.Equal("first line\r\nsecond line", result.GetProperty("output").GetString());
    }

    [Theory]
    [InlineData("not json", "invalid_json")]
    [InlineData("null", "invalid_request")]
    [InlineData("{\"version\":2,\"id\":\"x\",\"method\":\"status\",\"parameters\":{}}", "unsupported_version")]
    [InlineData("{\"version\":1,\"id\":\"\",\"method\":\"status\",\"parameters\":{}}", "invalid_request")]
    [InlineData("{\"version\":1,\"id\":\"x\",\"method\":\"\",\"parameters\":{}}", "invalid_request")]
    [InlineData("{\"version\":1,\"id\":\"x\",\"method\":\"status\",\"parameters\":[]}", "invalid_request")]
    public void InvalidRequest_ReturnsStructuredError(string json, string expectedCode)
    {
        Assert.False(LiveTestProtocol.TryDeserializeRequest(json, out _, out var error));

        Assert.NotNull(error);
        Assert.Equal(expectedCode, error.Code);
        Assert.False(error.OutcomeUncertain);
    }

    [Fact]
    public void OversizedRequest_IsRejectedBeforeDeserialization()
    {
        string json = new string('x', LiveTestProtocol.MaximumMessageBytes + 1);

        Assert.False(LiveTestProtocol.TryDeserializeRequest(json, out _, out var error));

        Assert.Equal("request_too_large", error.Code);
    }

    [Fact]
    public void OversizedResponse_CannotBeSerialized()
    {
        LiveTestResponse response = LiveTestResponse.Success(
            "request-1",
            Process(),
            new { output = new string('x', LiveTestProtocol.MaximumMessageBytes) });

        Assert.Throws<InvalidOperationException>(() => LiveTestProtocol.SerializeResponse(response));
    }

    private static LiveTestRequest Request(string method, string parametersJson)
    {
        using JsonDocument document = JsonDocument.Parse(parametersJson);
        return new LiveTestRequest
        {
            Version = LiveTestProtocol.Version,
            Id = Guid.NewGuid().ToString("N"),
            Method = method,
            Parameters = document.RootElement.Clone(),
        };
    }

    private static LiveTestProcessInfo Process()
    {
        return new LiveTestProcessInfo
        {
            Pid = 123,
            Role = "server",
            RunToken = "test-run",
        };
    }
}
