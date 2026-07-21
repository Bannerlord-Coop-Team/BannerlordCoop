using System;
using System.Text;
using System.Text.Json;

namespace Common.LiveTesting;

public static class LiveTestProtocol
{
    public const int Version = 1;
    public const int MaximumMessageBytes = 1024 * 1024;
    public const string PipePrefix = "BannerlordCoop.LiveTest.v1.";

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
    };

    public static string GetPipeName(int processId)
    {
        if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId));

        return PipePrefix + processId;
    }

    public static string SerializeRequest(LiveTestRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        return SerializeBounded(request);
    }

    public static string SerializeResponse(LiveTestResponse response)
    {
        if (response == null) throw new ArgumentNullException(nameof(response));

        return SerializeBounded(response);
    }

    public static bool TryDeserializeRequest(
        string json,
        out LiveTestRequest request,
        out LiveTestError error)
    {
        request = null;
        error = null;

        if (!IsWithinLimit(json))
        {
            error = new LiveTestError(
                "request_too_large",
                $"Live-test requests cannot exceed {MaximumMessageBytes} UTF-8 bytes.",
                false);
            return false;
        }

        try
        {
            request = JsonSerializer.Deserialize<LiveTestRequest>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            error = new LiveTestError("invalid_json", exception.Message, false);
            return false;
        }

        if (request == null)
        {
            error = new LiveTestError("invalid_request", "The request cannot be null.", false);
            return false;
        }

        if (request.Version != Version)
        {
            error = new LiveTestError(
                "unsupported_version",
                $"Protocol version {request.Version} is not supported; expected {Version}.",
                false);
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Id) || request.Id.Length > 128)
        {
            error = new LiveTestError(
                "invalid_request",
                "The request id must contain between 1 and 128 characters.",
                false);
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Method) || request.Method.Length > 64)
        {
            error = new LiveTestError(
                "invalid_request",
                "The request method must contain between 1 and 64 characters.",
                false);
            return false;
        }

        if (request.Parameters.ValueKind != JsonValueKind.Object)
        {
            error = new LiveTestError(
                "invalid_request",
                "The request parameters must be a JSON object.",
                false);
            return false;
        }

        return true;
    }

    public static bool TryDeserializeResponse(
        string json,
        out LiveTestResponse response,
        out LiveTestError error)
    {
        response = null;
        error = null;

        if (!IsWithinLimit(json))
        {
            error = new LiveTestError(
                "response_too_large",
                $"Live-test responses cannot exceed {MaximumMessageBytes} UTF-8 bytes.",
                false);
            return false;
        }

        try
        {
            response = JsonSerializer.Deserialize<LiveTestResponse>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            error = new LiveTestError("invalid_json", exception.Message, false);
            return false;
        }

        if (response == null || response.Version != Version || string.IsNullOrWhiteSpace(response.Id))
        {
            error = new LiveTestError("invalid_response", "The response envelope is invalid.", false);
            return false;
        }

        if (response.Process == null)
        {
            error = new LiveTestError("invalid_response", "The response has no process identity.", false);
            return false;
        }

        if ((response.Ok && response.Error != null) || (!response.Ok && response.Error == null))
        {
            error = new LiveTestError("invalid_response", "The response result and error state disagree.", false);
            return false;
        }

        return true;
    }

    private static string SerializeBounded<T>(T value)
    {
        string json = JsonSerializer.Serialize(value, JsonOptions);
        if (!IsWithinLimit(json))
        {
            throw new InvalidOperationException(
                $"The serialized live-test message exceeds {MaximumMessageBytes} UTF-8 bytes.");
        }

        return json;
    }

    private static bool IsWithinLimit(string json)
    {
        return json != null && Encoding.UTF8.GetByteCount(json) <= MaximumMessageBytes;
    }
}

public sealed class LiveTestRequest
{
    public int Version { get; set; }

    public string Id { get; set; }

    public string Method { get; set; }

    public JsonElement Parameters { get; set; }
}

public sealed class LiveTestResponse
{
    public int Version { get; set; } = LiveTestProtocol.Version;

    public string Id { get; set; }

    public bool Ok { get; set; }

    public LiveTestProcessInfo Process { get; set; }

    public object Result { get; set; }

    public LiveTestError Error { get; set; }

    public static LiveTestResponse Success(
        string id,
        LiveTestProcessInfo process,
        object result)
    {
        return new LiveTestResponse
        {
            Id = id,
            Ok = true,
            Process = process,
            Result = result,
        };
    }

    public static LiveTestResponse Failure(
        string id,
        LiveTestProcessInfo process,
        LiveTestError error)
    {
        if (error == null) throw new ArgumentNullException(nameof(error));

        return new LiveTestResponse
        {
            Id = id,
            Ok = false,
            Process = process,
            Error = error,
        };
    }
}

public sealed class LiveTestProcessInfo
{
    public int Pid { get; set; }

    public string Role { get; set; }

    public string PlatformId { get; set; }

    public string RunToken { get; set; }
}

public sealed class LiveTestError
{
    public LiveTestError()
    {
    }

    public LiveTestError(string code, string message, bool outcomeUncertain)
    {
        Code = code;
        Message = message;
        OutcomeUncertain = outcomeUncertain;
    }

    public string Code { get; set; }

    public string Message { get; set; }

    public bool OutcomeUncertain { get; set; }
}
