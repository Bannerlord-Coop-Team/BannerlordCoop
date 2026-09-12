using System;

namespace Common.Logging;

/// <summary>
/// Identifies the log file owned by the current BannerlordCoop process.
/// </summary>
/// <summary>Provides the active co-op log path for diagnostic capture.</summary>
public interface ICoopLogFile
{
    string Path { get; }
}

/// <inheritdoc />
public sealed class CoopLogFile : ICoopLogFile
{
    public string Path { get; }

    public CoopLogFile(string path)
    {
        Path = string.IsNullOrWhiteSpace(path) ? null : System.IO.Path.GetFullPath(path);
    }
}
