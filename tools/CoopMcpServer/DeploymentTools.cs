using System.ComponentModel;
using ModelContextProtocol.Server;

namespace CoopMcpServer;

public interface IDeploymentTools
{
    Task<DeploymentReport> DeployMod(string solution_path, string profile, CancellationToken cancellationToken, string configuration = "Release");
}

[McpServerToolType]
public sealed class DeploymentTools : IDeploymentTools
{
    private readonly IModDeploymentService deployment;
    public DeploymentTools(IModDeploymentService deployment) { this.deployment = deployment; }

    [McpServerTool(Name = "deploy_mod", UseStructuredContent = true), Description("Explicitly rebuild trusted source/Coop.sln mod projects (Release by default; Debug for live testing) and transactionally deploy a bounded file set to an existing deployment-enabled profile. Requires separate operator permission, idle games, durable backup root and Windows MSBuild. Never called by start_run. Preserves optin/config/XML unless profile config explicitly supplies subModuleXml. Returns manifest/backups/build log directory, hashes/MVIDs and rollback failures; inspect state, never retry unresolved restoration. MSBuild executes trusted repository code, not sandboxed input. No launch, shell arguments, whole-bin or DedicatedServer copying.")]
    public Task<DeploymentReport> DeployMod(string solution_path, string profile, CancellationToken cancellationToken, [Description("Build configuration: Release (default) or Debug only. Release does not include the live-test bridge.")] string configuration = "Release") =>
        deployment.DeployAsync(solution_path, profile, cancellationToken, configuration);
}
