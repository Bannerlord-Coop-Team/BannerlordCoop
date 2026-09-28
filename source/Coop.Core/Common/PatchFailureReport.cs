using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Coop.Core.Common;

/// <summary>
/// Turns a patching failure into text a player can act on
/// </summary>
public interface IPatchFailureReport
{
    /// <summary>
    /// Short popup text, naming the assembly, version and file when a load failed
    /// </summary>
    string Describe(Exception exception);

    /// <summary>
    /// Every loaded copy of the assembly that failed to load, for the log. Empty for other failures
    /// </summary>
    string ListLoadedCopies(Exception exception);
}

/// <inheritdoc cref="IPatchFailureReport"/>
public class PatchFailureReport : IPatchFailureReport
{
    internal const int MaxPopupLength = 300;
    internal const string Headline = "Coop could not apply its patches.";
    internal const string Fallback = Headline + " See the coop log for details.";

    // Load messages are localized, but the assembly display name inside them is not.
    private static readonly Regex DisplayName = new Regex(@"(?<name>[\w.\-]+), Version=(?<version>\d+(?:\.\d+){1,3})");

    private readonly Func<IEnumerable<Assembly>> loadedAssemblies;

    public PatchFailureReport() : this(() => AppDomain.CurrentDomain.GetAssemblies())
    {
    }

    internal PatchFailureReport(Func<IEnumerable<Assembly>> loadedAssemblies)
    {
        this.loadedAssemblies = loadedAssemblies;
    }

    public string Describe(Exception exception)
    {
        // Callers run this inside their own catch, so it must never throw.
        try
        {
            Exception cause = Unwrap(exception);
            string detail = TryGetFailedAssembly(cause, out string name, out Version version)
                ? DescribeLoadFailure(cause, name, version)
                : FirstLine(cause?.Message);

            if (string.IsNullOrEmpty(detail)) return Fallback;

            string text = $"{Headline} {detail}";
            return text.Length <= MaxPopupLength ? text : text.Substring(0, MaxPopupLength - 3) + "...";
        }
        catch (Exception)
        {
            return Fallback;
        }
    }

    public string ListLoadedCopies(Exception exception)
    {
        try
        {
            if (!TryGetFailedAssembly(Unwrap(exception), out string name, out _)) return string.Empty;

            var copies = FindLoadedCopies(name)
                .Select(copy => string.IsNullOrEmpty(copy.Location)
                    ? $"{copy.Version} in memory"
                    : $"{copy.Version} at {copy.Location}")
                .ToArray();

            return copies.Length == 0
                ? $"No copy of {name} is loaded"
                : $"Loaded copies of {name}: {string.Join("; ", copies)}";
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private string DescribeLoadFailure(Exception cause, string name, Version version)
    {
        string failure = cause is TypeLoadException
            ? $"Could not load a type from {name} {version}"
            : $"Could not load {name} {version}";

        string location = FindLoadedCopies(name).FirstOrDefault(copy => copy.Version == version).Location;

        return string.IsNullOrEmpty(location) ? $"{failure}." : $"{failure} at {location}.";
    }

    private List<(Version Version, string Location)> FindLoadedCopies(string name)
    {
        var copies = new List<(Version Version, string Location)>();

        foreach (Assembly assembly in loadedAssemblies())
        {
            try
            {
                AssemblyName assemblyName = assembly.GetName();
                if (!string.Equals(assemblyName.Name, name, StringComparison.OrdinalIgnoreCase)) continue;

                copies.Add((assemblyName.Version, assembly.Location));
            }
            catch (Exception)
            {
                // Location throws for dynamic assemblies, and one unreadable assembly must not hide the others.
            }
        }

        return copies;
    }

    private static bool TryGetFailedAssembly(Exception cause, out string name, out Version version)
    {
        name = null;
        version = null;

        if (!(cause is TypeLoadException ||
              cause is FileNotFoundException ||
              cause is FileLoadException ||
              cause is BadImageFormatException)) return false;

        Match match = DisplayName.Match(cause.Message ?? string.Empty);
        if (!match.Success || !Version.TryParse(match.Groups["version"].Value, out version)) return false;

        name = match.Groups["name"].Value;
        return true;
    }

    private static Exception Unwrap(Exception exception)
    {
        while (true)
        {
            Exception inner = exception switch
            {
                TargetInvocationException or TypeInitializationException or AggregateException => exception.InnerException,
                ReflectionTypeLoadException typeLoad => typeLoad.LoaderExceptions?.FirstOrDefault(loader => loader != null),
                _ => null,
            };

            if (inner == null) return exception;

            exception = inner;
        }
    }

    private static string FirstLine(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;

        int end = message.IndexOfAny(new[] { '\r', '\n' });
        return (end < 0 ? message : message.Substring(0, end)).Trim();
    }
}
