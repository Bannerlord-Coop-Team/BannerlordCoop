using Common.Extensions;
using Common.Logging;
using System.Reflection;

namespace Common.Tests.Extensions;

public class ReflectionExtensionsTests
{
    private class ThrowingNamespaceType : TypeDelegator
    {
        public ThrowingNamespaceType() : base(typeof(ReflectionExtensionsTests)) { }

        public override string? FullName => "Fake.OptionalDependencyType";

        public override string Namespace => throw new FileNotFoundException("Missing optional dependency");
    }

    [Fact]
    public void FilterLoadableTypes_SkipsTypeWhoseNamespaceThrows()
    {
        Type good = typeof(ReflectionExtensionsTests);
        Type[] all = { new ThrowingNamespaceType(), good };

        var result = ReflectionExtensions.FilterLoadableTypes("Fake", () => all, "Common.Tests");

        Assert.Equal(new[] { good }, result);
    }

    [Fact]
    public void FilterLoadableTypes_UsesLoadedTypesFromReflectionTypeLoadException()
    {
        Type good = typeof(ReflectionExtensionsTests);
        Type[] partial = { good, null!, new ThrowingNamespaceType() };

        var result = ReflectionExtensions.FilterLoadableTypes(
            "Fake",
            () => throw new ReflectionTypeLoadException(partial, new Exception[] { new TypeLoadException(), new TypeLoadException(), new TypeLoadException() }),
            "");

        Assert.Equal(new[] { good }, result);
    }

    [Fact]
    public void FilterLoadableTypes_ReturnsEmptyWhenGetTypesFailsOutright()
    {
        var result = ReflectionExtensions.FilterLoadableTypes("Fake", () => throw new FileNotFoundException(), "");

        Assert.Empty(result);
    }

    [Fact]
    public void FilterLoadableTypes_AppliesNamespacePrefix()
    {
        var result = ReflectionExtensions.FilterLoadableTypes(
            "Fake",
            () => new[] { typeof(ReflectionExtensionsTests), typeof(string) },
            "System");

        Assert.Equal(new[] { typeof(string) }, result);
    }

    [Fact]
    public void FilterLoadableTypes_WarningNamesSkippedTypesAndDistinctLoaderErrors()
    {
        const string assemblyName = "Fake.Diagnostics.Assembly";
        var lines = new List<string>();
        void Capture(string line)
        {
            lock (lines) lines.Add(line);
        }

        Type good = typeof(ReflectionExtensionsTests);
        Type[] partial = { good, null!, new ThrowingNamespaceType() };
        var loaderErrors = Enumerable.Range(0, 8)
            .Select(i => (Exception)new TypeLoadException("Could not load Optional.Dependency " + (i % 7)))
            .ToArray();

        OutputSinkManager.AddLogCallback(Capture);
        try
        {
            ReflectionExtensions.FilterLoadableTypes(
                assemblyName,
                () => throw new ReflectionTypeLoadException(partial, loaderErrors),
                "");
        }
        finally
        {
            OutputSinkManager.RemoveLogCallback(Capture);
        }

        string warning;
        lock (lines) warning = Assert.Single(lines, line => line.Contains(assemblyName));
        Assert.Contains("OptionalDependencyType", warning);
        Assert.Contains("Could not load Optional.Dependency 0", warning);
        Assert.Contains("Could not load Optional.Dependency 4", warning);
        Assert.DoesNotContain("Could not load Optional.Dependency 5", warning);
        Assert.Equal(1, warning.Split("Could not load Optional.Dependency 0").Length - 1);
    }
}
