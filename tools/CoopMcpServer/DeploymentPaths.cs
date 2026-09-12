namespace CoopMcpServer;

public interface IDeploymentPaths
{
    bool Within(string path, string root);
    void NoLinks(string path);
    string DurableRoot(string root);
}

public sealed class DeploymentPaths : IDeploymentPaths
{
    public bool Within(string path, string root) => Path.GetFullPath(path).StartsWith(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    public void NoLinks(string path)
    {
        for (string current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("Deployment paths must not traverse reparse points: " + current);
        }
    }

    public string DurableRoot(string root)
    {
        if (!Path.IsPathFullyQualified(root ?? "")) throw new ArgumentException("deployment.durableRoot must be absolute and outside Temp.");
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        NoLinks(root);
        var temporaryRoots = new[] { Path.GetTempPath(), Environment.GetEnvironmentVariable("TEMP"), Environment.GetEnvironmentVariable("TMP"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp") };
        if (temporaryRoots.Where(p => !string.IsNullOrWhiteSpace(p)).Any(p => Within(root, p) ||
            string.Equals(root, Path.TrimEndingDirectorySeparator(Path.GetFullPath(p)), StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("deployment.durableRoot must be outside Temp.");
        return root;
    }
}
