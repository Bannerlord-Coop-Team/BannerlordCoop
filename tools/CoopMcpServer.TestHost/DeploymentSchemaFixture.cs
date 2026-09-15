using CoopMcpServer;

namespace CoopMcpServer.TestHost;

// Output schema fixture only: no build, filesystem mutation, or game process.
public sealed class DeploymentSchemaFixture : IModDeploymentService
{
    public Task<DeploymentReport> DeployAsync(string solution, string profile, CancellationToken cancellationToken, string configuration = "Release") => Task.FromResult(new DeploymentReport
    {
        Configuration = configuration, DeploymentId = "fixture", State = profile, ArtifactDirectory = "fixture artifacts", Solution = solution, Module = "fixture module",
        Files = profile == "failed_before_apply" ? new() : new()
        {
            new DeploymentEntry { Source = "fixture input", Target = "fixture target", Staged = "fixture stage", Backup = "fixture backup",
                OriginallyExisted = false, Original = null, Replacement = new FileEvidence(42, "fixture sha256", Guid.Empty.ToString()),
                ApplyAttempted = true, Restoration = profile == "rollback_failed" ? "failed: fixture" : "not_required" },
        },
        UnresolvedRestoration = profile == "rollback_failed" ? new() { "fixture restoration failed" } : new(),
    });
}
