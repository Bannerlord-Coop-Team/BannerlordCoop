namespace VerificationHarness.DedicatedServerSynthetic;

public interface IDedicatedServerSyntheticScenarioRunner
{
    Task<DedicatedServerSyntheticScenarioResult> RunAsync(
        DedicatedServerSyntheticOptions options,
        CancellationToken cancellationToken);
}

internal interface IDedicatedServerSyntheticClientSession
{
    Task<bool> Ready { get; }
    Task<DedicatedServerSyntheticNodeResult> RunAsync(CancellationToken cancellationToken);
    void Release();
}

internal interface IDedicatedServerSyntheticClientFactory
{
    IDedicatedServerSyntheticClientSession Create(
        DedicatedServerSyntheticNodeOptions options,
        bool holdConnection);
}

internal sealed class DedicatedServerSyntheticClientFactory
    : IDedicatedServerSyntheticClientFactory
{
    public IDedicatedServerSyntheticClientSession Create(
        DedicatedServerSyntheticNodeOptions options,
        bool holdConnection)
    {
        return new DedicatedServerSyntheticClientNode(options, holdConnection);
    }
}

public sealed class DedicatedServerSyntheticScenarioResult
{
    public bool Attempted { get; set; }
    public bool Completed { get; set; }
    public bool WrongPasswordRejected { get; set; }
    public bool IncompatibleModuleRejected { get; set; }
    public bool CompatibleModuleHandshakeCompleted { get; set; }
    public bool ProtocolShortcut { get; set; }
    public DedicatedModuleValidationContract? ModuleValidation { get; set; }
    public List<DedicatedServerSyntheticLifecycleSnapshot> Lifecycle { get; set; } = new();
    public List<DedicatedServerSyntheticNodeResult> Clients { get; set; } = new();
    public List<string> WireHashes { get; set; } = new();
    public List<string> FailureCodes { get; set; } = new();
}

public sealed class DedicatedServerSyntheticRuntimeScenario
    : IDedicatedServerSyntheticScenarioRunner
{
    private readonly IDedicatedServerControlClient controlClient;
    private readonly IDedicatedServerControlResponseValidator responseValidator;
    private readonly IDedicatedServerSyntheticClientFactory clientFactory;

    public DedicatedServerSyntheticRuntimeScenario(
        IDedicatedServerControlClient controlClient,
        IDedicatedServerControlResponseValidator responseValidator)
        : this(
            controlClient,
            responseValidator,
            new DedicatedServerSyntheticClientFactory())
    {
    }

    internal DedicatedServerSyntheticRuntimeScenario(
        IDedicatedServerControlClient controlClient,
        IDedicatedServerControlResponseValidator responseValidator,
        IDedicatedServerSyntheticClientFactory clientFactory)
    {
        if (controlClient == null) throw new ArgumentNullException(nameof(controlClient));
        if (responseValidator == null) throw new ArgumentNullException(nameof(responseValidator));
        if (clientFactory == null) throw new ArgumentNullException(nameof(clientFactory));
        this.controlClient = controlClient;
        this.responseValidator = responseValidator;
        this.clientFactory = clientFactory;
    }

    public async Task<DedicatedServerSyntheticScenarioResult> RunAsync(
        DedicatedServerSyntheticOptions options,
        CancellationToken cancellationToken)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        var result = new DedicatedServerSyntheticScenarioResult { Attempted = true };
        var heldClients = new List<IDedicatedServerSyntheticClientSession>();
        var heldClientTasks = new List<Task<DedicatedServerSyntheticNodeResult>>();
        using var scenarioCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        scenarioCancellation.CancelAfter(options.TimeoutMilliseconds);

        try
        {
            DedicatedServerSyntheticLifecycleSnapshot beforeConnect =
                await WaitForSnapshotAsync(
                    options,
                    "before-connect",
                    Array.Empty<string>(),
                    null,
                    scenarioCancellation.Token);
            result.Lifecycle.Add(beforeConnect);
            result.ModuleValidation = beforeConnect.ModuleValidation;

            DedicatedServerSyntheticNodeResult wrongPassword =
                await RunWrongPasswordProbeAsync(options, scenarioCancellation.Token);
            result.Clients.Add(wrongPassword);
            result.WireHashes.AddRange(wrongPassword.WireHashes);
            result.WrongPasswordRejected =
                wrongPassword.Success &&
                wrongPassword.AcceptedConnections == 0 &&
                wrongPassword.RejectedPasswords == 1;
            if (!result.WrongPasswordRejected)
            {
                result.FailureCodes.Add("wrong-password-negative-control-failed");
            }

            DedicatedServerSyntheticNodeResult incompatibleModule =
                await RunModuleMismatchProbeAsync(
                    options,
                    beforeConnect.ModuleValidation,
                    scenarioCancellation.Token);
            result.Clients.Add(incompatibleModule);
            result.WireHashes.AddRange(incompatibleModule.WireHashes);
            result.IncompatibleModuleRejected =
                incompatibleModule.Success &&
                incompatibleModule.ModuleDenialsObserved == 1 &&
                incompatibleModule.ModuleMatchesObserved == 0 &&
                incompatibleModule.FreshControllerResultsObserved == 0;
            if (!result.IncompatibleModuleRejected)
            {
                result.FailureCodes.Add("incompatible-module-negative-control-failed");
            }

            IDedicatedServerSyntheticClientSession clientA = CreateHeldClient(
                options,
                beforeConnect.ModuleValidation,
                DedicatedServerSyntheticOptions.ExpectedControllerIds[0],
                "initial-a");
            IDedicatedServerSyntheticClientSession clientB = CreateHeldClient(
                options,
                beforeConnect.ModuleValidation,
                DedicatedServerSyntheticOptions.ExpectedControllerIds[1],
                "initial-b");
            heldClients.Add(clientA);
            heldClients.Add(clientB);
            Task<DedicatedServerSyntheticNodeResult> clientATask =
                clientA.RunAsync(scenarioCancellation.Token);
            Task<DedicatedServerSyntheticNodeResult> clientBTask =
                clientB.RunAsync(scenarioCancellation.Token);
            heldClientTasks.Add(clientATask);
            heldClientTasks.Add(clientBTask);
            await RequireReadyAsync(
                new[] { clientA, clientB },
                scenarioCancellation.Token);

            DedicatedServerSyntheticLifecycleSnapshot twoConnected =
                await WaitForSnapshotAsync(
                    options,
                    "two-connected",
                    DedicatedServerSyntheticOptions.ExpectedControllerIds,
                    beforeConnect.ModuleValidation,
                    scenarioCancellation.Token);
            result.Lifecycle.Add(twoConnected);
            string firstConnectionA = GetConnectionInstanceId(
                twoConnected,
                DedicatedServerSyntheticOptions.ExpectedControllerIds[0]);

            clientA.Release();
            DedicatedServerSyntheticNodeResult releasedA =
                await clientATask.WaitAsync(scenarioCancellation.Token);
            AddClientResult(result, releasedA);
            DedicatedServerSyntheticLifecycleSnapshot oneDisconnected =
                await WaitForSnapshotAsync(
                    options,
                    "one-disconnected",
                    new[] { DedicatedServerSyntheticOptions.ExpectedControllerIds[1] },
                    beforeConnect.ModuleValidation,
                    scenarioCancellation.Token);
            result.Lifecycle.Add(oneDisconnected);

            IDedicatedServerSyntheticClientSession reconnectedA = CreateHeldClient(
                options,
                beforeConnect.ModuleValidation,
                DedicatedServerSyntheticOptions.ExpectedControllerIds[0],
                "reconnected-a");
            heldClients.Add(reconnectedA);
            Task<DedicatedServerSyntheticNodeResult> reconnectedATask =
                reconnectedA.RunAsync(scenarioCancellation.Token);
            heldClientTasks.Add(reconnectedATask);
            await RequireReadyAsync(new[] { reconnectedA }, scenarioCancellation.Token);

            DedicatedServerSyntheticLifecycleSnapshot reconnected =
                await WaitForSnapshotAsync(
                    options,
                    "reconnected",
                    DedicatedServerSyntheticOptions.ExpectedControllerIds,
                    beforeConnect.ModuleValidation,
                    scenarioCancellation.Token);
            result.Lifecycle.Add(reconnected);
            string secondConnectionA = GetConnectionInstanceId(
                reconnected,
                DedicatedServerSyntheticOptions.ExpectedControllerIds[0]);
            if (string.Equals(firstConnectionA, secondConnectionA, StringComparison.Ordinal) ||
                reconnected.ConnectionRoster.Any(entry =>
                    string.Equals(
                        entry.ConnectionInstanceId,
                        firstConnectionA,
                        StringComparison.Ordinal)))
            {
                throw new DedicatedServerSyntheticScenarioException(
                    "reconnect-reused-connection-instance");
            }

            result.ProtocolShortcut = true;
        }
        catch (DedicatedServerSyntheticScenarioException exception)
        {
            result.FailureCodes.Add(exception.FailureCode);
        }
        catch (OperationCanceledException)
        {
            result.FailureCodes.Add(
                cancellationToken.IsCancellationRequested
                    ? "runtime-scenario-cancelled"
                    : "runtime-scenario-timeout");
        }
        catch (Exception)
        {
            result.FailureCodes.Add("runtime-scenario-failed");
        }
        finally
        {
            foreach (IDedicatedServerSyntheticClientSession client in heldClients)
            {
                client.Release();
            }

            using var cleanupCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cleanupCancellation.CancelAfter(TimeSpan.FromSeconds(5));
            foreach (Task<DedicatedServerSyntheticNodeResult> task in heldClientTasks)
            {
                if (task.IsCompletedSuccessfully)
                {
                    AddClientResult(result, task.Result);
                    continue;
                }

                try
                {
                    AddClientResult(
                        result,
                        await task.WaitAsync(cleanupCancellation.Token));
                }
                catch (Exception)
                {
                    result.FailureCodes.Add("synthetic-client-cleanup-failed");
                }
            }

            try
            {
                DedicatedServerSyntheticLifecycleSnapshot finalEmpty =
                    await WaitForSnapshotAsync(
                        options,
                        "final-empty",
                        Array.Empty<string>(),
                        result.ModuleValidation,
                        cleanupCancellation.Token);
                result.Lifecycle.Add(finalEmpty);
            }
            catch (Exception)
            {
                result.FailureCodes.Add("final-empty-roster-not-observed");
            }
        }

        result.FailureCodes = result.FailureCodes
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        result.WireHashes = result.WireHashes
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        DedicatedServerSyntheticNodeResult[] compatibleClients = result.Clients
            .Where(client => string.Equals(
                client.Scenario,
                DedicatedServerSyntheticNodeOptions.BaselineScenario,
                StringComparison.Ordinal))
            .ToArray();
        result.CompatibleModuleHandshakeCompleted =
            compatibleClients.Length == 3 &&
            compatibleClients.All(client =>
                client.Success &&
                client.ModuleMatchesObserved == 1 &&
                client.ModuleDenialsObserved == 0 &&
                client.FreshControllerResultsObserved == 1);
        if (!result.CompatibleModuleHandshakeCompleted)
        {
            result.FailureCodes.Add("compatible-module-handshake-failed");
        }
        result.FailureCodes = result.FailureCodes
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        result.Completed =
            result.WrongPasswordRejected &&
            result.IncompatibleModuleRejected &&
            result.CompatibleModuleHandshakeCompleted &&
            result.ProtocolShortcut &&
            result.Lifecycle.Select(snapshot => snapshot.Phase).SequenceEqual(
                new[]
                {
                    "before-connect",
                    "two-connected",
                    "one-disconnected",
                    "reconnected",
                    "final-empty"
                },
                StringComparer.Ordinal) &&
            result.Clients.Count == 5 &&
            result.Clients.All(client => client.Success) &&
            result.FailureCodes.Count == 0;
        return result;
    }

    private async Task<DedicatedServerSyntheticNodeResult> RunModuleMismatchProbeAsync(
        DedicatedServerSyntheticOptions options,
        DedicatedModuleValidationContract moduleValidation,
        CancellationToken cancellationToken)
    {
        DedicatedServerSyntheticNodeOptions nodeOptions = CreateClientOptions(
            options,
            moduleValidation,
            DedicatedServerSyntheticOptions.ExpectedControllerIds[0],
            DedicatedServerSyntheticNodeOptions.ModuleMismatchScenario,
            "module-mismatch");
        return await clientFactory.Create(nodeOptions, holdConnection: false)
            .RunAsync(cancellationToken);
    }

    private async Task<DedicatedServerSyntheticNodeResult> RunWrongPasswordProbeAsync(
        DedicatedServerSyntheticOptions options,
        CancellationToken cancellationToken)
    {
        DedicatedServerSyntheticNodeOptions nodeOptions = CreateClientOptions(
            options,
            null,
            DedicatedServerSyntheticOptions.ExpectedControllerIds[0],
            DedicatedServerSyntheticNodeOptions.WrongPasswordScenario,
            "wrong-password");
        return await clientFactory.Create(nodeOptions, holdConnection: false)
            .RunAsync(cancellationToken);
    }

    private IDedicatedServerSyntheticClientSession CreateHeldClient(
        DedicatedServerSyntheticOptions options,
        DedicatedModuleValidationContract moduleValidation,
        string controllerId,
        string requestSuffix)
    {
        DedicatedServerSyntheticNodeOptions nodeOptions = CreateClientOptions(
            options,
            moduleValidation,
            controllerId,
            DedicatedServerSyntheticNodeOptions.BaselineScenario,
            requestSuffix);
        return clientFactory.Create(nodeOptions, holdConnection: true);
    }

    private static DedicatedServerSyntheticNodeOptions CreateClientOptions(
        DedicatedServerSyntheticOptions options,
        DedicatedModuleValidationContract? moduleValidation,
        string controllerId,
        string scenario,
        string requestSuffix)
    {
        var arguments = new List<string>
        {
            "--role", "client",
            "--scenario", scenario,
            "--port", options.JoinPort.ToString(),
            "--timeout-ms", options.TimeoutMilliseconds.ToString(),
            "--run-token", options.RunToken,
            "--request-id", CreateRequestId(options.RequestId, requestSuffix, 0),
            "--controller-id", controllerId,
            "--password-env", options.PasswordEnvironmentVariable
        };
        if (moduleValidation != null)
        {
            arguments.Add("--module-contract");
            arguments.Add(DedicatedServerSyntheticNodeOptions.EncodeModuleValidationContract(
                moduleValidation));
        }

        return DedicatedServerSyntheticNodeOptions.Parse(arguments.ToArray());
    }

    private static async Task RequireReadyAsync(
        IReadOnlyList<IDedicatedServerSyntheticClientSession> clients,
        CancellationToken cancellationToken)
    {
        bool[] ready = await Task.WhenAll(clients.Select(client => client.Ready))
            .WaitAsync(cancellationToken);
        if (ready.Any(value => !value))
        {
            throw new DedicatedServerSyntheticScenarioException(
                "synthetic-client-protocol-failed");
        }
    }

    private async Task<DedicatedServerSyntheticLifecycleSnapshot> WaitForSnapshotAsync(
        DedicatedServerSyntheticOptions options,
        string phase,
        IReadOnlyCollection<string> expectedControllerIds,
        DedicatedModuleValidationContract? expectedModuleValidation,
        CancellationToken cancellationToken)
    {
        DedicatedServerControlValidation? lastValidation = null;
        int attempt = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            attempt++;
            string requestId = CreateRequestId(options.RequestId, phase, attempt);
            try
            {
                string responseJson = await controlClient.GetStatusAsync(
                    options.ServerProcessId,
                    requestId,
                    TimeSpan.FromMilliseconds(
                        Math.Min(1000, options.TimeoutMilliseconds)),
                    cancellationToken);
                lastValidation = responseValidator.Validate(
                    responseJson,
                    new DedicatedServerControlExpectation(
                        options.ServerProcessId,
                        options.RunToken,
                        requestId,
                        options.JoinPort,
                        expectedControllerIds));
                if (lastValidation.IsValid && lastValidation.Snapshot != null)
                {
                    if (expectedModuleValidation != null &&
                        !DedicatedModuleValidationContracts.Equivalent(
                            expectedModuleValidation,
                            lastValidation.Snapshot.ModuleValidation))
                    {
                        lastValidation.FailureCodes.Add("module-validation-contract-changed");
                        await Task.Delay(25, cancellationToken);
                        continue;
                    }

                    return new DedicatedServerSyntheticLifecycleSnapshot
                    {
                        Phase = phase,
                        ControlEnvelopeValidated = lastValidation.EnvelopeValid,
                        ControlRequestIdentityValidated = lastValidation.RequestIdentityValid,
                        DedicatedProcessIdentityValidated = lastValidation.ProcessIdentityValid,
                        FirstClassConnectionRosterValidated = lastValidation.RosterSurfaceValid,
                        Serving = lastValidation.Snapshot.Serving,
                        JoinPort = lastValidation.Snapshot.JoinPort,
                        ModuleValidation = lastValidation.Snapshot.ModuleValidation,
                        ConnectionRoster = lastValidation.Snapshot.ConnectionRoster.ToList()
                    };
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }
            catch (IOException)
            {
            }

            await Task.Delay(25, cancellationToken);
        }

        string failureCode = lastValidation?.FailureCodes.LastOrDefault() ??
            "control-snapshot-unavailable";
        throw new DedicatedServerSyntheticScenarioException(
            phase + "-" + failureCode);
    }

    private static string GetConnectionInstanceId(
        DedicatedServerSyntheticLifecycleSnapshot snapshot,
        string controllerId)
    {
        DedicatedServerRosterEntry? entry = snapshot.ConnectionRoster.SingleOrDefault(value =>
            string.Equals(value.ControllerId, controllerId, StringComparison.Ordinal));
        if (entry == null)
        {
            throw new DedicatedServerSyntheticScenarioException(
                "expected-controller-missing-from-snapshot");
        }

        return entry.ConnectionInstanceId;
    }

    private static string CreateRequestId(string baseRequestId, string suffix, int attempt)
    {
        string tail = attempt > 0 ? $"-{suffix}-{attempt}" : $"-{suffix}";
        int maximumBaseLength = Math.Max(1, 128 - tail.Length);
        string prefix = baseRequestId.Length <= maximumBaseLength
            ? baseRequestId
            : baseRequestId.Substring(0, maximumBaseLength);
        return prefix + tail;
    }

    private static void AddClientResult(
        DedicatedServerSyntheticScenarioResult scenario,
        DedicatedServerSyntheticNodeResult client)
    {
        if (scenario.Clients.Contains(client))
        {
            return;
        }

        scenario.Clients.Add(client);
        scenario.WireHashes.AddRange(client.WireHashes);
        scenario.FailureCodes.AddRange(client.FailureCodes);
    }

    private sealed class DedicatedServerSyntheticScenarioException : Exception
    {
        public DedicatedServerSyntheticScenarioException(string failureCode)
        {
            FailureCode = failureCode;
        }

        public string FailureCode { get; }
    }
}
