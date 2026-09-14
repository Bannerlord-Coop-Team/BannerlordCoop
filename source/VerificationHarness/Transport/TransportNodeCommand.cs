using System.Text.Json;

using VerificationHarness.Serialization;

namespace VerificationHarness.Transport;

public static class TransportNodeCommand
{
    public const int NodeFailureExitCode = 7;

    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        if (args == null) throw new ArgumentNullException(nameof(args));
        if (output == null) throw new ArgumentNullException(nameof(output));

        TransportNodeOptions options = TransportNodeOptions.Parse(args);
        TransportNodeResult result;
        try
        {
            if (string.Equals(options.Role, "server", StringComparison.Ordinal))
            {
                result = await new TransportServerNode(options, output).RunAsync(cancellationToken);
            }
            else
            {
                result = await new TransportClientNode(options).RunAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            result = new TransportNodeResult
            {
                Role = options.Role,
                InstanceId = options.InstanceId,
                Seed = options.Seed,
                ProcessId = Environment.ProcessId,
                RuntimeIdentity = ProcessRuntimeIdentity.CaptureCurrent(),
                Success = false,
                Error = $"node-exception:{ex.GetType().Name}"
            };
        }

        await output.WriteLineAsync(JsonSerializer.Serialize(result, TransportJson.Options));
        await output.FlushAsync();
        return result.Success ? 0 : NodeFailureExitCode;
    }
}

internal static class TransportJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}

internal static class TransportScenarios
{
    public const string Converge = "converge";
    public const string Diverge = "diverge";
    public const string Reconnect = "reconnect";
    public const string Malformed = "malformed";
    public const string OutOfSequence = "out-of-sequence";
    public const string CorruptAcknowledgement = "corrupt-acknowledgement";
    public const string Timeout = "timeout";

    public static bool IsKnown(string scenario)
    {
        return scenario is Converge or Diverge or Reconnect or Malformed or OutOfSequence or CorruptAcknowledgement or Timeout;
    }

    public static bool IsNegativeProtocolCase(string scenario)
    {
        return scenario is Malformed or OutOfSequence or CorruptAcknowledgement;
    }

    public static bool IsPreStateNegativeProtocolCase(string scenario)
    {
        return scenario is Malformed or OutOfSequence;
    }

    public static string? ExpectedRejectionCode(string scenario)
    {
        return scenario switch
        {
            Malformed => "malformed-frame",
            OutOfSequence => "invalid-sequence",
            CorruptAcknowledgement => "digest-mismatch",
            _ => null
        };
    }
}

internal sealed class TransportNodeOptions
{
    public string Role { get; private set; } = string.Empty;
    public string InstanceId { get; private set; } = string.Empty;
    public int Port { get; private set; }
    public string Scenario { get; private set; } = string.Empty;
    public int TimeoutMilliseconds { get; private set; }
    public string Seed { get; private set; } = VerificationSeed.Default;

    public static TransportNodeOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException("Transport node options must be --name <value> pairs.");
            }

            if (!values.TryAdd(args[index], args[index + 1]))
            {
                throw new ArgumentException($"Duplicate transport node option: {args[index]}.");
            }
        }

        string role = Required(values, "--role");
        if (role is not "server" and not "client")
        {
            throw new ArgumentException("Transport node role must be server or client.");
        }

        string instanceId = Required(values, "--instance-id");
        if (instanceId is not "server" and not "client-a" and not "client-b")
        {
            throw new ArgumentException("Transport node instance id is not part of the fixed process-peer topology.");
        }

        if (!int.TryParse(Required(values, "--port"), out int port) ||
            !((port is >= 1024 and <= 65535) || (role == "server" && port == 0)))
        {
            throw new ArgumentException("Transport node port must be zero for server auto-allocation or between 1024 and 65535.");
        }

        string scenario = Required(values, "--scenario");
        if (!TransportScenarios.IsKnown(scenario))
        {
            throw new ArgumentException($"Unknown process-peer scenario: {scenario}.");
        }

        if (!int.TryParse(Required(values, "--timeout-ms"), out int timeoutMilliseconds) ||
            timeoutMilliseconds is < 250 or > 120000)
        {
            throw new ArgumentException("Transport node timeout must be between 250 and 120000 milliseconds.");
        }

        string seed = VerificationSeed.Normalize(Required(values, "--seed"), "transport-node");

        var known = new[] { "--role", "--instance-id", "--port", "--scenario", "--timeout-ms", "--seed" };
        string? unknown = values.Keys.FirstOrDefault(x => !known.Contains(x, StringComparer.Ordinal));
        if (unknown != null) throw new ArgumentException($"Unknown transport node option: {unknown}.");

        return new TransportNodeOptions
        {
            Role = role,
            InstanceId = instanceId,
            Port = port,
            Scenario = scenario,
            TimeoutMilliseconds = timeoutMilliseconds,
            Seed = seed
        };
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string option)
    {
        if (!values.TryGetValue(option, out string? value) || string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Missing value for {option}.");
        }

        return value;
    }
}
