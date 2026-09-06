using System.Text.Json;

namespace CoopMcpServer;

public sealed class CoopMcpServerSettings
{
    public string ArtifactDirectory { get; set; }
    public Dictionary<string, LaunchProfile> Profiles { get; set; } = new();

    public static CoopMcpServerSettings Load(string path)
    {
        var settings = JsonSerializer.Deserialize<CoopMcpServerSettings>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (settings == null || !Path.IsPathFullyQualified(settings.ArtifactDirectory ?? ""))
            throw new ArgumentException("artifactDirectory must be an absolute path.");
        return settings;
    }
}

public sealed class LaunchProfile
{
    public string Executable { get; set; }
    public string[] Modules { get; set; } = { "Native", "SandBoxCore", "SandBox", "StoryMode", "Coop" };
    public string ServerPlatformId { get; set; } = "testserver";
    public string[] ClientPlatformIds { get; set; } = { "testclient1", "testclient2" };

    public void Validate(int clientCount)
    {
        if (clientCount < 0 || clientCount > 16 || ClientPlatformIds == null || clientCount > ClientPlatformIds.Length)
            throw new ArgumentException("client_count must be 0..16 and fit the profile's clientPlatformIds.");
        if (!Path.IsPathFullyQualified(Executable ?? "") ||
            !string.Equals(Path.GetFileName(Executable), "Bannerlord.exe", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(Executable))
            throw new ArgumentException("The in-game profile requires an existing absolute Bannerlord.exe path.");
        var ids = new[] { ServerPlatformId }.Concat(ClientPlatformIds.Take(clientCount)).ToArray();
        if (ids.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 64 ||
                id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_')) ||
            ids.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ids.Length)
            throw new ArgumentException("All selected platform IDs must be distinct, 1..64 ASCII letters/digits, '-' or '_'.");
        if (Modules == null || Modules.Length == 0 || Modules.Length > 32 ||
            Modules.Any(m => string.IsNullOrWhiteSpace(m) || m.Length > 128 ||
                m.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-')) ||
            !Modules.Contains("Coop", StringComparer.Ordinal))
            throw new ArgumentException("modules must include Coop and contain only module identifiers.");
    }
}
