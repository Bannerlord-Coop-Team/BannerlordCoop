using Common.Logging;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace GameInterface.Services.Modules;

/// <summary>
/// Loads a coop naval assembly shipped next to the coop assemblies, so a stale, partial or corrupt file reads as unloadable instead of throwing.
/// </summary>
public interface INavalAssemblyLoader
{
    /// <summary>Loads <paramref name="file"/> and resolves all of its types; false when the file is missing or fails to load.</summary>
    bool TryLoad(string file, out Assembly assembly);
}

/// <inheritdoc cref="INavalAssemblyLoader"/>
public class NavalAssemblyLoader : INavalAssemblyLoader
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalAssemblyLoader>();

    private readonly string assemblyDirectory;

    // Takes the folder the naval assemblies ship in, the coop bin folder at runtime.
    public NavalAssemblyLoader(string assemblyDirectory)
    {
        this.assemblyDirectory = assemblyDirectory;
    }

    /// <inheritdoc/>
    public bool TryLoad(string file, out Assembly assembly)
    {
        assembly = null;
        string path = Path.Combine(assemblyDirectory, file);
        if (!File.Exists(path)) return false;

        try
        {
            var loaded = Assembly.LoadFrom(path);
            // Resolving every type surfaces a missing or mismatched dependency here instead of during module registration.
            loaded.GetTypes();
            assembly = loaded;
            return true;
        }
        catch (Exception e) when (IsLoadFailure(e))
        {
            Logger.Warning("Could not load {File}: {Reason}", file, Describe(e));
            return false;
        }
    }

    // The failures a bad image, a missing dependency or an unreadable file raise while loading.
    private static bool IsLoadFailure(Exception e) =>
        e is BadImageFormatException
        || e is IOException
        || e is UnauthorizedAccessException
        || e is ReflectionTypeLoadException
        || e is TypeLoadException;

    // The load error, with the first loader exception when a type failed to resolve.
    private static string Describe(Exception e)
    {
        if (!(e is ReflectionTypeLoadException typeLoad)) return e.Message;

        var first = typeLoad.LoaderExceptions.FirstOrDefault(loaderException => loaderException != null);
        return first?.Message ?? e.Message;
    }
}
