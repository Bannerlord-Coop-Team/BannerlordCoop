using System;
using System.Collections.Generic;
using TaleWorlds.Engine;

namespace GameInterface.Utils.Commands;

public interface IRglCommandLineRegistry
{
    void RegisterCommand(string fullName);
}

/// <summary>Registers command names with the native RGL command line.</summary>
public sealed class RglCommandLineRegistry : IRglCommandLineRegistry
{
    // RGL has no remove API, so names remain registered across coop containers.
    private static readonly object registrationLock = new object();
    private static readonly HashSet<string> registeredCommands =
        new HashSet<string>(StringComparer.Ordinal);

    private readonly Action<string> registerCommand;
    private readonly Func<bool> isEngineAvailable;

    public RglCommandLineRegistry()
        : this(
            Utilities.AddCommandLineFunction,
            () => EngineApplicationInterface.IUtil != null)
    {
    }

    internal RglCommandLineRegistry(
        Action<string> registerCommand,
        Func<bool> isEngineAvailable = null)
    {
        if (registerCommand == null) throw new ArgumentNullException(nameof(registerCommand));

        this.registerCommand = registerCommand;
        this.isEngineAvailable = isEngineAvailable ?? (() => true);
    }

    public void RegisterCommand(string fullName)
    {
        if (fullName == null) throw new ArgumentNullException(nameof(fullName));
        if (!isEngineAvailable()) return;

        lock (registrationLock)
        {
            if (registeredCommands.Contains(fullName)) return;

            registerCommand(fullName);
            registeredCommands.Add(fullName);
        }
    }
}
