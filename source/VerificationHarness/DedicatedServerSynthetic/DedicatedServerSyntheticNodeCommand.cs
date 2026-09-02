using Common.Network;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace VerificationHarness.DedicatedServerSynthetic;

public static class DedicatedServerSyntheticNodeCommand
{
    public const int NodeFailureExitCode = 7;

    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        if (args == null) throw new ArgumentNullException(nameof(args));
        if (output == null) throw new ArgumentNullException(nameof(output));

        DedicatedServerSyntheticNodeOptions options = DedicatedServerSyntheticNodeOptions.Parse(args);
        DedicatedServerSyntheticNodeResult result;
        try
        {
            DedicatedServerSyntheticNodeBase node = options.Role == "server"
                ? new DedicatedServerSyntheticServerNode(options, output)
                : new DedicatedServerSyntheticClientNode(options);
            result = await node.RunAsync(cancellationToken);
        }
        catch (Exception)
        {
            result = DedicatedServerSyntheticNodeBase.CreateInitialResult(options);
            result.FailureCodes.Add("node-exception");
        }

        result.FailureCodes = result.FailureCodes
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        result.WireHashes = result.WireHashes
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        await output.WriteLineAsync(JsonSerializer.Serialize(result, DedicatedServerSyntheticJson.Options));
        await output.FlushAsync();
        return result.Success ? 0 : NodeFailureExitCode;
    }
}

public sealed class DedicatedServerSyntheticNodeOptions
{
    public const string BaselineScenario = "baseline";
    public const string WrongPasswordScenario = "wrong-password";

    public string Role { get; private set; } = string.Empty;
    public string Scenario { get; private set; } = string.Empty;
    public int Port { get; private set; }
    public int TimeoutMilliseconds { get; private set; }
    public string RunToken { get; private set; } = string.Empty;
    public string RequestId { get; private set; } = string.Empty;
    public string ControllerId { get; private set; } = string.Empty;
    public int ExpectedClients { get; private set; }
    public string PasswordEnvironmentVariable { get; private set; } = string.Empty;
    internal string Password { get; private set; } = string.Empty;

    public static DedicatedServerSyntheticNodeOptions Parse(string[] args)
    {
        if (args == null) throw new ArgumentNullException(nameof(args));
        if (args.Length % 2 != 0)
        {
            throw new ArgumentException("DS synthetic node options must be --name <value> pairs.");
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index += 2)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal) ||
                !values.TryAdd(args[index], args[index + 1]))
            {
                throw new ArgumentException($"Invalid or duplicate DS synthetic node option: {args[index]}.");
            }
        }

        string role = Required(values, "--role");
        if (role is not "server" and not "client")
        {
            throw new ArgumentException("DS synthetic node role must be server or client.");
        }

        string scenario = Required(values, "--scenario");
        if (scenario is not BaselineScenario and not WrongPasswordScenario)
        {
            throw new ArgumentException("DS synthetic node scenario must be baseline or wrong-password.");
        }

        int port = RequiredInt(values, "--port", 1024, 65535);
        int timeout = RequiredInt(values, "--timeout-ms", 250, 120000);
        string runToken = RequiredToken(values, "--run-token", 64);
        string requestId = RequiredToken(values, "--request-id", 128);
        string passwordEnvironmentVariable = RequiredToken(values, "--password-env", 128, allowPeriod: false);
        string password = Environment.GetEnvironmentVariable(passwordEnvironmentVariable) ?? string.Empty;
        if (!ConnectionPassword.IsValid(password))
        {
            throw new ArgumentException(
                $"The password from --password-env exceeds {ConnectionPassword.MaxLength} characters.");
        }

        string controllerId = values.GetValueOrDefault("--controller-id", string.Empty);
        bool hasExpectedClients = values.ContainsKey("--expected-clients");
        int expectedClients = hasExpectedClients
            ? RequiredInt(values, "--expected-clients", 1, 2)
            : 0;
        if (role == "client")
        {
            if (!DedicatedServerSyntheticOptions.ExpectedControllerIds.Contains(controllerId, StringComparer.Ordinal))
            {
                throw new ArgumentException("Client controller id is not part of the fixed two-client topology.");
            }

            if (hasExpectedClients)
            {
                throw new ArgumentException("A client node does not accept --expected-clients.");
            }
        }
        else if (!string.IsNullOrEmpty(controllerId))
        {
            throw new ArgumentException("The server node does not accept --controller-id.");
        }

        if (role == "server")
        {
            int requiredClients = scenario == BaselineScenario ? 2 : 1;
            if (!hasExpectedClients || expectedClients != requiredClients)
            {
                throw new ArgumentException(
                    $"The {scenario} server requires --expected-clients {requiredClients}.");
            }
        }

        if (scenario == WrongPasswordScenario && string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("The wrong-password scenario requires a configured password.");
        }

        string[] known =
        {
            "--role", "--scenario", "--port", "--timeout-ms", "--run-token", "--request-id",
            "--controller-id", "--expected-clients", "--password-env"
        };
        string? unknown = values.Keys.FirstOrDefault(x => !known.Contains(x, StringComparer.Ordinal));
        if (unknown != null) throw new ArgumentException($"Unknown DS synthetic node option: {unknown}.");

        return new DedicatedServerSyntheticNodeOptions
        {
            Role = role,
            Scenario = scenario,
            Port = port,
            TimeoutMilliseconds = timeout,
            RunToken = runToken,
            RequestId = requestId,
            ControllerId = controllerId,
            ExpectedClients = expectedClients,
            PasswordEnvironmentVariable = passwordEnvironmentVariable,
            Password = password
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

    private static string RequiredToken(
        IReadOnlyDictionary<string, string> values,
        string option,
        int maximumLength,
        bool allowPeriod = true)
    {
        string value = Required(values, option);
        bool invalidCharacter = value.Any(character =>
            !char.IsLetterOrDigit(character) &&
            character is not '_' and not '-' &&
            (!allowPeriod || character != '.'));
        if (value.Length > maximumLength || invalidCharacter)
        {
            throw new ArgumentException($"{option} contains unsupported characters or is too long.");
        }

        return value;
    }

    private static int RequiredInt(
        IReadOnlyDictionary<string, string> values,
        string option,
        int minimum,
        int maximum)
    {
        string value = Required(values, option);
        if (!int.TryParse(value, out int parsed) || parsed < minimum || parsed > maximum)
        {
            throw new ArgumentException($"{option} must be between {minimum} and {maximum}.");
        }

        return parsed;
    }
}

internal abstract class DedicatedServerSyntheticNodeBase : INetEventListener
{
    private const int MaximumFailureCodes = 128;
    private const int MaximumWireHashes = 512;
    protected readonly DedicatedServerSyntheticNodeOptions Options;
    protected readonly IDedicatedServerWireCodec Codec = new DedicatedServerWireCodec();
    protected readonly DedicatedServerSyntheticNodeResult Result;

    protected DedicatedServerSyntheticNodeBase(DedicatedServerSyntheticNodeOptions options)
    {
        Options = options;
        Result = CreateInitialResult(options);
    }

    public abstract Task<DedicatedServerSyntheticNodeResult> RunAsync(CancellationToken cancellationToken);

    public static DedicatedServerSyntheticNodeResult CreateInitialResult(
        DedicatedServerSyntheticNodeOptions options)
    {
        return new DedicatedServerSyntheticNodeResult
        {
            Role = options.Role,
            Scenario = options.Scenario,
            RequestId = options.RequestId,
            RunToken = options.RunToken,
            ProcessId = Environment.ProcessId,
            PasswordConfigured = !string.IsNullOrEmpty(options.Password)
        };
    }

    protected void RecordWire(byte[] bytes)
    {
        if (Result.WireHashes.Count >= MaximumWireHashes)
        {
            AddFailure("wire-hash-limit-reached");
            return;
        }

        Result.WireHashes.Add(
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    protected void AddFailure(string failureCode)
    {
        if (Result.FailureCodes.Count < MaximumFailureCodes)
        {
            Result.FailureCodes.Add(failureCode);
        }
        else if (!Result.FailureCodes.Contains("failure-code-limit-reached", StringComparer.Ordinal))
        {
            Result.FailureCodes.Add("failure-code-limit-reached");
        }
    }

    protected static NetManager CreateManager(INetEventListener listener)
    {
        return new NetManager(listener)
        {
            ChannelsCount = 2,
            DisconnectTimeout = 60000,
            UpdateTime = 15
        };
    }

    protected static async Task PollAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(10, cancellationToken);
    }

    public abstract void OnPeerConnected(NetPeer peer);
    public abstract void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo);
    public abstract void OnNetworkReceive(
        NetPeer peer,
        NetPacketReader reader,
        byte channelNumber,
        DeliveryMethod deliveryMethod);
    public abstract void OnConnectionRequest(ConnectionRequest request);

    public virtual void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
    {
        AddFailure("network-error");
    }

    public virtual void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {
    }

    public virtual void OnNetworkReceiveUnconnected(
        IPEndPoint remoteEndPoint,
        NetPacketReader reader,
        UnconnectedMessageType messageType)
    {
    }
}

internal sealed class DedicatedServerSyntheticServerNode : DedicatedServerSyntheticNodeBase
{
    private readonly TextWriter output;
    private readonly Dictionary<NetPeer, PeerProbeState> peerStates = new();
    private readonly Stopwatch clock = new();
    private long? completeAfterMilliseconds;

    public DedicatedServerSyntheticServerNode(
        DedicatedServerSyntheticNodeOptions options,
        TextWriter output)
        : base(options)
    {
        this.output = output;
    }

    public override async Task<DedicatedServerSyntheticNodeResult> RunAsync(
        CancellationToken cancellationToken)
    {
        NetManager manager = CreateManager(this);
        if (!manager.Start(Options.Port))
        {
            AddFailure("server-bind-failed");
            return Result;
        }

        try
        {
            await output.WriteLineAsync(JsonSerializer.Serialize(
                new DedicatedServerSyntheticReadyEvent
                {
                    RequestId = Options.RequestId,
                    RunToken = Options.RunToken,
                    ProcessId = Environment.ProcessId,
                    Port = manager.LocalPort
                },
                DedicatedServerSyntheticJson.Options));
            await output.FlushAsync();

            clock.Restart();
            while (!cancellationToken.IsCancellationRequested &&
                   clock.ElapsedMilliseconds < Options.TimeoutMilliseconds)
            {
                manager.PollEvents();
                if (IsLogicalOutcomeComplete())
                {
                    completeAfterMilliseconds ??= clock.ElapsedMilliseconds + 150;
                    if (clock.ElapsedMilliseconds >= completeAfterMilliseconds)
                    {
                        Result.Success = true;
                        return Result;
                    }
                }

                await PollAsync(cancellationToken);
            }

            AddFailure(
                cancellationToken.IsCancellationRequested ? "server-cancelled" : "server-timeout");
            return Result;
        }
        finally
        {
            manager.DisconnectAll();
            manager.Stop();
        }
    }

    public override void OnConnectionRequest(ConnectionRequest request)
    {
        string suppliedPassword;
        try
        {
            suppliedPassword = request.Data.GetString(ConnectionPassword.MaxLength);
        }
        catch (Exception)
        {
            RejectPassword(request);
            return;
        }

        if (!ConnectionPassword.IsAccepted(Options.Password, suppliedPassword))
        {
            RejectPassword(request);
            return;
        }

        request.Accept();
    }

    public override void OnPeerConnected(NetPeer peer)
    {
        Result.AcceptedConnections++;
        if (peerStates.Count >= Options.ExpectedClients)
        {
            AddFailure("unexpected-peer-count");
            peer.Disconnect();
            return;
        }

        if (!peerStates.TryAdd(peer, new PeerProbeState()))
        {
            AddFailure("duplicate-peer-connection");
            peer.Disconnect();
            return;
        }

        byte[] heartbeat = Codec.EncodeCampaignTime(1, -1);
        RecordWire(heartbeat);
        peer.Send(heartbeat, 0, DeliveryMethod.Sequenced);
    }

    public override void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        Result.Disconnections++;
    }

    public override void OnNetworkReceive(
        NetPeer peer,
        NetPacketReader reader,
        byte channelNumber,
        DeliveryMethod deliveryMethod)
    {
        byte[] wireBytes = reader.GetRemainingBytes();
        RecordWire(wireBytes);
        try
        {
            HandleFrame(peer, wireBytes, channelNumber, deliveryMethod);
        }
        catch (Exception)
        {
            AddFailure("invalid-client-wire-frame");
            peer.Disconnect();
        }
    }

    private void HandleFrame(
        NetPeer peer,
        byte[] wireBytes,
        byte channelNumber,
        DeliveryMethod deliveryMethod)
    {
        DedicatedServerWireFrame frame = Codec.DecodeFrame(wireBytes);
        if (channelNumber != frame.ManifestEntry.Channel ||
            !string.Equals(deliveryMethod.ToString(), frame.ManifestEntry.DeliveryMethod, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Client frame used the wrong LiteNetLib lane.");
        }

        if (frame.ManifestEntry.TypeId == DedicatedServerWireManifest.AggregateMessagePacketTypeId)
        {
            foreach (byte[] nested in Codec.DecodeAggregate(wireBytes))
            {
                HandleFrame(peer, nested, 0, DeliveryMethod.ReliableOrdered);
            }

            return;
        }

        if (frame.ManifestEntry.TypeId == DedicatedServerWireManifest.NetworkModuleVersionsValidateTypeId)
        {
            if (!peerStates.TryGetValue(peer, out PeerProbeState? peerState) || peerState.ModuleRequestObserved)
            {
                throw new InvalidDataException("Synthetic peer sent a duplicate or unbound module request.");
            }

            DedicatedModuleValidationRequest request = Codec.DecodeModuleValidationRequest(wireBytes);
            if (request.ModuleCount != 0 || request.ClientBuildVersion != "ds-synthetic-intentional-mismatch")
            {
                throw new InvalidDataException("Synthetic module request is not the frozen mismatch probe.");
            }

            peerState.ModuleRequestObserved = true;
            byte[] response = Codec.EncodeModuleValidationResult(
                false,
                "intentional synthetic build mismatch",
                "ds-synthetic-server-build");
            RecordWire(response);
            peer.Send(response, 0, DeliveryMethod.ReliableOrdered);
            return;
        }

        if (frame.ManifestEntry.TypeId == DedicatedServerWireManifest.NetworkClientValidateTypeId)
        {
            if (!peerStates.TryGetValue(peer, out PeerProbeState? peerState) || peerState.ControllerId != null)
            {
                throw new InvalidDataException("Synthetic peer sent a duplicate or unbound controller request.");
            }

            string controllerId = Codec.DecodeClientValidationRequest(wireBytes);
            if (!DedicatedServerSyntheticOptions.ExpectedControllerIds.Contains(controllerId, StringComparer.Ordinal))
            {
                throw new InvalidDataException("Synthetic controller is not in the frozen topology.");
            }

            if (peerStates.Values.Any(x => string.Equals(x.ControllerId, controllerId, StringComparison.Ordinal)))
            {
                throw new InvalidDataException("Synthetic controller id was reused by another peer.");
            }

            peerState.ControllerId = controllerId;
            Result.ProtocolShortcut = true;
            byte[] response = Codec.EncodeFreshClientValidationResult();
            RecordWire(response);
            peer.Send(response, 0, DeliveryMethod.ReliableOrdered);
            return;
        }

        throw new InvalidDataException("Client sent a type outside the safe synthetic subset.");
    }

    private void RejectPassword(ConnectionRequest request)
    {
        Result.RejectedPasswords++;
        var reason = new NetDataWriter();
        reason.Put((byte)ConnectionRejectCode.IncorrectPassword);
        request.Reject(reason);
    }

    private bool IsLogicalOutcomeComplete()
    {
        return Options.Scenario == DedicatedServerSyntheticNodeOptions.WrongPasswordScenario
            ? Result.RejectedPasswords >= 1 && Result.AcceptedConnections == 0
            : Result.AcceptedConnections == Options.ExpectedClients &&
              peerStates.Count == Options.ExpectedClients &&
              peerStates.Values.All(x => x.ModuleRequestObserved && x.ControllerId != null) &&
              peerStates.Values.Select(x => x.ControllerId).ToHashSet(StringComparer.Ordinal)
                  .SetEquals(DedicatedServerSyntheticOptions.ExpectedControllerIds) &&
              Result.FailureCodes.Count == 0;
    }

    private sealed class PeerProbeState
    {
        public bool ModuleRequestObserved { get; set; }
        public string? ControllerId { get; set; }
    }
}

internal sealed class DedicatedServerSyntheticClientNode : DedicatedServerSyntheticNodeBase
{
    private NetPeer? serverPeer;
    private bool connected;
    private bool completionPending;
    private readonly Stopwatch clock = new();
    private long completeAfterMilliseconds;

    public DedicatedServerSyntheticClientNode(DedicatedServerSyntheticNodeOptions options)
        : base(options)
    {
    }

    public override async Task<DedicatedServerSyntheticNodeResult> RunAsync(
        CancellationToken cancellationToken)
    {
        NetManager manager = CreateManager(this);
        if (!manager.Start())
        {
            AddFailure("client-start-failed");
            return Result;
        }

        try
        {
            string suppliedPassword = Options.Scenario == DedicatedServerSyntheticNodeOptions.WrongPasswordScenario
                ? CreateWrongPassword(Options.Password)
                : Options.Password;
            serverPeer = manager.Connect("127.0.0.1", Options.Port, suppliedPassword);
            clock.Restart();
            while (!cancellationToken.IsCancellationRequested &&
                   clock.ElapsedMilliseconds < Options.TimeoutMilliseconds)
            {
                manager.PollEvents();
                if (completionPending && clock.ElapsedMilliseconds >= completeAfterMilliseconds)
                {
                    serverPeer?.Disconnect();
                    Result.Success = IsExpectedOutcomeComplete();
                    if (!Result.Success) AddFailure("client-outcome-incomplete");
                    return Result;
                }

                await PollAsync(cancellationToken);
            }

            AddFailure(
                cancellationToken.IsCancellationRequested ? "client-cancelled" : "client-timeout");
            return Result;
        }
        finally
        {
            manager.Stop();
        }
    }

    public override void OnConnectionRequest(ConnectionRequest request)
    {
        request.Reject();
    }

    public override void OnPeerConnected(NetPeer peer)
    {
        connected = true;
        Result.AcceptedConnections++;
        byte[] moduleRequest = Codec.EncodeModuleMismatchRequest("ds-synthetic-intentional-mismatch");
        byte[] clientRequest = Codec.EncodeClientValidationRequest(Options.ControllerId);
        RecordWire(moduleRequest);
        RecordWire(clientRequest);
        peer.Send(moduleRequest, 0, DeliveryMethod.ReliableOrdered);
        peer.Send(clientRequest, 0, DeliveryMethod.ReliableOrdered);
    }

    public override void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        Result.Disconnections++;
        serverPeer = null;
        if (Options.Scenario == DedicatedServerSyntheticNodeOptions.WrongPasswordScenario && !connected)
        {
            NetPacketReader data = disconnectInfo.AdditionalData;
            try
            {
                if (disconnectInfo.Reason == DisconnectReason.ConnectionRejected &&
                    data != null &&
                    !data.IsNull &&
                    data.TryGetByte(out byte code) &&
                    code == (byte)ConnectionRejectCode.IncorrectPassword)
                {
                    Result.RejectedPasswords++;
                }
                else
                {
                    AddFailure("wrong-password-reject-code-missing");
                }
            }
            finally
            {
                if (data != null && !data.IsNull) data.Recycle();
            }

            completionPending = true;
            completeAfterMilliseconds = clock.ElapsedMilliseconds + 25;
            return;
        }

        if (!completionPending)
        {
            AddFailure("unexpected-disconnect");
            completionPending = true;
            completeAfterMilliseconds = clock.ElapsedMilliseconds;
        }
    }

    public override void OnNetworkReceive(
        NetPeer peer,
        NetPacketReader reader,
        byte channelNumber,
        DeliveryMethod deliveryMethod)
    {
        byte[] wireBytes = reader.GetRemainingBytes();
        RecordWire(wireBytes);
        try
        {
            HandleFrame(wireBytes, channelNumber, deliveryMethod);
        }
        catch (Exception)
        {
            AddFailure("invalid-server-wire-frame");
            completionPending = true;
            completeAfterMilliseconds = clock.ElapsedMilliseconds;
        }

        if (Result.HeartbeatsObserved == 1 &&
            Result.ModuleDenialsObserved == 1 &&
            Result.FreshControllerResultsObserved == 1)
        {
            completionPending = true;
            completeAfterMilliseconds = clock.ElapsedMilliseconds + 75;
        }
    }

    private void HandleFrame(byte[] wireBytes, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        DedicatedServerWireFrame frame = Codec.DecodeFrame(wireBytes);
        if (channelNumber != frame.ManifestEntry.Channel ||
            !string.Equals(deliveryMethod.ToString(), frame.ManifestEntry.DeliveryMethod, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Server frame used the wrong LiteNetLib lane.");
        }

        if (frame.ManifestEntry.TypeId == DedicatedServerWireManifest.AggregateMessagePacketTypeId)
        {
            foreach (byte[] nested in Codec.DecodeAggregate(wireBytes))
            {
                HandleFrame(nested, 0, DeliveryMethod.ReliableOrdered);
            }

            return;
        }

        if (frame.ManifestEntry.TypeId == DedicatedServerWireManifest.CampaignTimePacketTypeId)
        {
            Codec.DecodeCampaignTime(wireBytes);
            Result.HeartbeatsObserved++;
            return;
        }

        if (frame.ManifestEntry.TypeId == DedicatedServerWireManifest.NetworkModuleVersionsValidatedTypeId)
        {
            DedicatedModuleValidationResult validation = Codec.DecodeModuleValidationResult(wireBytes);
            if (validation.Matches) throw new InvalidDataException("Mismatch control was unexpectedly accepted.");
            Result.ModuleDenialsObserved++;
            return;
        }

        if (frame.ManifestEntry.TypeId == DedicatedServerWireManifest.NetworkClientValidatedTypeId)
        {
            DedicatedClientValidationResult validation = Codec.DecodeClientValidationResult(wireBytes);
            if (validation.HeroExists || validation.PlayerPayloadPresent)
            {
                throw new InvalidDataException("Fresh-controller shortcut returned an existing player.");
            }

            Result.FreshControllerResultsObserved++;
            Result.ProtocolShortcut = true;
            return;
        }

        throw new InvalidDataException("Server sent a type outside the safe synthetic subset.");
    }

    private bool IsExpectedOutcomeComplete()
    {
        if (Options.Scenario == DedicatedServerSyntheticNodeOptions.WrongPasswordScenario)
        {
            return !connected &&
                   Result.RejectedPasswords == 1 &&
                   Result.FailureCodes.Count == 0;
        }

        return Result.HeartbeatsObserved == 1 &&
               Result.ModuleDenialsObserved == 1 &&
               Result.FreshControllerResultsObserved == 1 &&
               Result.ProtocolShortcut &&
               Result.FailureCodes.Count == 0;
    }

    internal static string CreateWrongPassword(string password)
    {
        const string knownWrongPassword = "ds-synthetic-known-wrong-password";
        return string.Equals(password, knownWrongPassword, StringComparison.Ordinal)
            ? knownWrongPassword + "-x"
            : knownWrongPassword;
    }
}
