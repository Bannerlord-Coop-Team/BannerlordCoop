using System;
using System.IO;

namespace Coop.Core.Common.Configuration;

/// <summary>
/// Finds an operator file for the server: its own variable, then COOP_DATA_DIR, then the
/// server-data folder beside the dedicated-server engine directory.
/// </summary>
public interface IServerDataPath
{
    string Resolve(string fileEnvironmentVariable, string fileName);
}

/// <inheritdoc cref="IServerDataPath"/>
internal sealed class ServerDataPath : IServerDataPath
{
    public string Resolve(string fileEnvironmentVariable, string fileName)
    {
        return Resolve(
            Environment.GetEnvironmentVariable(fileEnvironmentVariable),
            Environment.GetEnvironmentVariable("COOP_DATA_DIR"),
            AppContext.BaseDirectory,
            fileName);
    }

    internal static string Resolve(
        string? configuredPath,
        string? coopDataDirectory,
        string applicationBaseDirectory,
        string fileName)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath)) return Path.GetFullPath(configuredPath);

        if (!string.IsNullOrWhiteSpace(coopDataDirectory))
        {
            return Path.Combine(coopDataDirectory, fileName);
        }

        return Path.GetFullPath(Path.Combine(
            applicationBaseDirectory,
            "..",
            "..",
            "..",
            "server-data",
            fileName));
    }
}
