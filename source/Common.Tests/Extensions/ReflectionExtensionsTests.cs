using Common.Extensions;
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
    public void FilterLoadableTypes_PartialLoad_KeepsTheTypesThatLoaded()
    {
        Type good = typeof(ReflectionExtensionsTests);
        Type[] partial = { good, null!, new ThrowingNamespaceType() };

        var result = ReflectionExtensions.FilterLoadableTypes(
            "Fake",
            () => throw new ReflectionTypeLoadException(partial, new Exception[] { new TypeLoadException(), new TypeLoadException(), new TypeLoadException() }),
            "");

        Assert.Equal(new[] { good }, result);
    }
}
