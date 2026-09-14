using VerificationHarness.DedicatedServerSynthetic;
using VerificationHarness.Planning;
using VerificationHarness.Transport;

namespace VerificationHarness;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                WriteUsage();
                return 2;
            }

            if (string.Equals(args[0], "plan", StringComparison.Ordinal))
            {
                return await RunPlanner(args.Skip(1).ToArray());
            }

            if (string.Equals(args[0], "process-peer", StringComparison.Ordinal))
            {
                return await new ProcessPeerController().RunAsync(
                    args.Skip(1).ToArray(),
                    Console.Out,
                    CancellationToken.None);
            }

            if (string.Equals(args[0], "validate-plan", StringComparison.Ordinal))
            {
                return await RunPlanValidation(args.Skip(1).ToArray());
            }

            if (string.Equals(args[0], "process-peer-manifest", StringComparison.Ordinal))
            {
                return await RunProcessPeerManifest(args.Skip(1).ToArray());
            }

            if (string.Equals(args[0], "process-peer-suite", StringComparison.Ordinal))
            {
                return await new ProcessPeerSuiteController().RunAsync(
                    args.Skip(1).ToArray(),
                    Console.Out,
                    CancellationToken.None);
            }

            if (string.Equals(args[0], "transport-node", StringComparison.Ordinal))
            {
                return await TransportNodeCommand.RunAsync(
                    args.Skip(1).ToArray(),
                    Console.Out,
                    CancellationToken.None);
            }

            if (string.Equals(args[0], "dedicated-server-synthetic", StringComparison.Ordinal))
            {
                return await new DedicatedServerSyntheticController().RunAsync(
                    args.Skip(1).ToArray(),
                    Console.Out,
                    CancellationToken.None);
            }

            if (string.Equals(args[0], "dedicated-server-synthetic-node", StringComparison.Ordinal))
            {
                return await DedicatedServerSyntheticNodeCommand.RunAsync(
                    args.Skip(1).ToArray(),
                    Console.Out,
                    CancellationToken.None);
            }

            Console.Error.WriteLine($"Unknown command: {args[0]}");
            WriteUsage();
            return 2;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task<int> RunPlanner(string[] args)
    {
        string? head = null;
        string? syntheticTree = null;
        bool readStdin = false;
        var argumentPaths = new List<string?>();

        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            switch (argument)
            {
                case "--head":
                    head = ReadOptionValue(args, ref index, argument);
                    break;
                case "--tree":
                    syntheticTree = ReadOptionValue(args, ref index, argument);
                    break;
                case "--stdin":
                    readStdin = true;
                    break;
                default:
                    if (argument.StartsWith('-'))
                    {
                        throw new ArgumentException($"Unknown plan option: {argument}");
                    }

                    argumentPaths.Add(argument);
                    break;
            }
        }

        if (head == null || syntheticTree == null)
        {
            throw new ArgumentException("The plan command requires --head <40-hex> and --tree <40-hex>.");
        }

        if (readStdin && argumentPaths.Count > 0)
        {
            throw new ArgumentException("The plan command cannot combine --stdin with path arguments.");
        }

        if (readStdin)
        {
            string? line;
            while ((line = await Console.In.ReadLineAsync()) != null)
            {
                argumentPaths.Add(line);
            }
        }

        var source = new VerificationSourceIdentity(head, syntheticTree);
        IVerificationPlanBuilder builder = new VerificationPlanBuilder();
        VerificationPlan plan = builder.Build(source, argumentPaths);
        IVerificationPlanWriter writer = new VerificationPlanWriter();
        await Console.Out.WriteAsync(writer.Serialize(plan));
        await Console.Out.WriteAsync('\n');

        return plan.InputValid ? 0 : 3;
    }

    private static string ReadOptionValue(string[] args, ref int index, string option)
    {
        index++;
        if (index >= args.Length)
        {
            throw new ArgumentException($"Missing value for {option}.");
        }

        return args[index];
    }

    private static async Task<int> RunPlanValidation(string[] args)
    {
        if (args.Length != 12)
            throw new ArgumentException(
                "The validate-plan command requires --plan <json-path> --head <40-hex> --tree <40-hex> --base <40-hex> --changed-paths <newline-list-path> --output <json-path>.");

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index += 2)
        {
            if (!values.TryAdd(args[index], args[index + 1]))
                throw new ArgumentException($"Duplicate validate-plan option: {args[index]}.");
        }

        string[] expected = { "--base", "--changed-paths", "--head", "--output", "--plan", "--tree" };
        if (!values.Keys.OrderBy(item => item, StringComparer.Ordinal).SequenceEqual(expected, StringComparer.Ordinal))
            throw new ArgumentException("Unknown validate-plan option.");
        if (!File.Exists(values["--plan"]))
            throw new ArgumentException("The validate-plan input file does not exist.");
        if (!File.Exists(values["--changed-paths"]))
            throw new ArgumentException("The validate-plan changed-paths file does not exist.");

        VerificationPlanReceipt receipt = new VerificationPlanValidator().Validate(
            await File.ReadAllTextAsync(values["--plan"]),
            values["--head"],
            values["--tree"],
            values["--base"],
            await File.ReadAllLinesAsync(values["--changed-paths"]));
        string json = System.Text.Json.JsonSerializer.Serialize(receipt, TransportJson.Options);
        await TransportEvidenceFileWriter.WriteAtomicallyAsync(values["--output"], json);
        await Console.Out.WriteLineAsync(json);
        return 0;
    }

    private static async Task<int> RunProcessPeerManifest(string[] args)
    {
        if (args.Length != 6)
            throw new ArgumentException(
                "The process-peer-manifest command requires --head <40-hex> --tree <40-hex> --output <json-path>.");

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index += 2)
        {
            if (!values.TryAdd(args[index], args[index + 1]))
                throw new ArgumentException($"Duplicate process-peer-manifest option: {args[index]}.");
        }

        string[] expected = { "--head", "--output", "--tree" };
        if (!values.Keys.OrderBy(item => item, StringComparer.Ordinal).SequenceEqual(expected, StringComparer.Ordinal))
            throw new ArgumentException("Unknown process-peer-manifest option.");

        ProcessPeerArtifactManifest manifest = await ProcessPeerArtifactManifestFile.CreateCurrentAsync(
            values["--head"],
            values["--tree"],
            values["--output"]);
        await Console.Out.WriteLineAsync(manifest.ManifestDigest);
        return 0;
    }

    private static void WriteUsage()
    {
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  VerificationHarness plan --head <40-hex> --tree <40-hex> <repository-path> [repository-path ...]");
        Console.Error.WriteLine("  VerificationHarness plan --head <40-hex> --tree <40-hex> --stdin");
        Console.Error.WriteLine("  VerificationHarness validate-plan --plan <json-path> --head <40-hex> --tree <40-hex> --base <40-hex> --changed-paths <newline-list-path> --output <json-path>");
        Console.Error.WriteLine("  VerificationHarness process-peer-manifest --head <40-hex> --tree <40-hex> --output <json-path>");
        Console.Error.WriteLine("  VerificationHarness process-peer --head <40-hex> --tree <40-hex> --artifact-manifest <json-path> [--scenario converge|diverge|reconnect|malformed|out-of-sequence|corrupt-acknowledgement|timeout] [--timeout-ms <milliseconds>] [--seed <non-negative-decimal|0x16-hex>] [--output <json-path>]");
        Console.Error.WriteLine("  VerificationHarness process-peer-suite --head <40-hex> --tree <40-hex> --artifact-manifest <json-path> [--timeout-ms <milliseconds>] [--seed <non-negative-decimal|0x16-hex>] [--output <json-path>]");
        Console.Error.WriteLine("  VerificationHarness dedicated-server-synthetic --head <40-hex> --tree <40-hex> --server-head <40-hex> --server-tree <40-hex> --server-pid <pid> --run-token <token> --request-id <id> --join-port <port> --password-env <name> --artifact-manifest <json-path> --artifact-manifest-sha256 <sha256> --artifact-root <staged-runtime-path> [--timeout-ms <milliseconds>] [--seed <non-negative-decimal|0x16-hex>] [--output <json-path>]");
        Console.Error.WriteLine("  VerificationHarness dedicated-server-synthetic-node --role server --scenario baseline --port <port> --timeout-ms <milliseconds> --run-token <token> --request-id <id> --password-env <name> --module-contract <base64-json-contract> --expected-clients 2");
        Console.Error.WriteLine("  VerificationHarness dedicated-server-synthetic-node --role server --scenario module-mismatch --port <port> --timeout-ms <milliseconds> --run-token <token> --request-id <id> --password-env <name> --module-contract <base64-json-contract> --expected-clients 1");
        Console.Error.WriteLine("  VerificationHarness dedicated-server-synthetic-node --role server --scenario wrong-password --port <port> --timeout-ms <milliseconds> --run-token <token> --request-id <id> --password-env <name> --expected-clients 1");
        Console.Error.WriteLine("  VerificationHarness dedicated-server-synthetic-node --role client --scenario baseline --port <port> --timeout-ms <milliseconds> --run-token <token> --request-id <id> --password-env <name> --controller-id <ds-synthetic-client-a|ds-synthetic-client-b> --module-contract <base64-json-contract>");
        Console.Error.WriteLine("  VerificationHarness dedicated-server-synthetic-node --role client --scenario module-mismatch --port <port> --timeout-ms <milliseconds> --run-token <token> --request-id <id> --password-env <name> --controller-id <ds-synthetic-client-a|ds-synthetic-client-b> --module-contract <base64-json-contract>");
        Console.Error.WriteLine("  VerificationHarness dedicated-server-synthetic-node --role client --scenario wrong-password --port <port> --timeout-ms <milliseconds> --run-token <token> --request-id <id> --password-env <name> --controller-id <ds-synthetic-client-a|ds-synthetic-client-b>");
    }
}
