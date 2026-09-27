using Coop.Core.Common;
using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace Coop.Tests;

public class PatchFailureReportTests
{
    private const string Immutable = "System.Collections.Immutable";
    private const string StrayPath = @"D:\Bannerlord\bin\Win64_Shipping_Client\System.Collections.Immutable.dll";
    private const string CoopPath = @"D:\Bannerlord\Modules\Coop\bin\Win64_Shipping_Client\System.Collections.Immutable.dll";

    // The message from the #3196 logs.
    private static readonly TypeLoadException ImmutableTypeLoad = new TypeLoadException(
        "Could not load type 'System.Runtime.InteropServices.ImmutableCollectionsMarshal' from assembly " +
        "'System.Collections.Immutable, Version=1.2.5.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'.");

    private static PatchFailureReport ReportWith(params Assembly[] loaded) => new PatchFailureReport(() => loaded);

    [Fact]
    public void Describe_TypeLoadFailure_NamesTheAssemblyVersionAndFile()
    {
        var report = ReportWith(
            new FakeAssembly(Immutable, "9.0.0.0", CoopPath),
            new FakeAssembly(Immutable, "1.2.5.0", StrayPath));

        Assert.Equal(
            $"Coop could not apply its patches. Could not load a type from {Immutable} 1.2.5.0 at {StrayPath}.",
            report.Describe(ImmutableTypeLoad));
    }

    [Fact]
    public void Describe_UnwrapsToTheInnermostLoadFailure()
    {
        var fileNotFound = new FileNotFoundException(
            "Could not load file or assembly 'System.Collections.Immutable, Version=8.0.0.0, Culture=neutral, " +
            "PublicKeyToken=b03f5f7f11d50a3a' or one of its dependencies.");
        var wrapped = new TypeInitializationException("AutoSyncBuilder",
            new AggregateException(new TargetInvocationException(
                new ReflectionTypeLoadException(new Type?[] { null }, new Exception?[] { null, fileNotFound }))));

        Assert.Equal(
            $"Coop could not apply its patches. Could not load {Immutable} 8.0.0.0.",
            ReportWith().Describe(wrapped));
    }

    [Fact]
    public void Describe_LocalizedLoadMessage_StillNamesTheAssembly()
    {
        var german = new TypeLoadException(
            "Der Typ \"System.Runtime.InteropServices.ImmutableCollectionsMarshal\" in der Assembly " +
            "\"System.Collections.Immutable, Version=1.2.5.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a\" " +
            "konnte nicht geladen werden.");

        Assert.Contains($"{Immutable} 1.2.5.0", ReportWith().Describe(german));
    }

    [Fact]
    public void Describe_OtherFailure_UsesTheFirstLineOfItsMessage()
    {
        var failure = new InvalidOperationException("Patching failed earlier in this session.\r\n   at somewhere");

        Assert.Equal(
            "Coop could not apply its patches. Patching failed earlier in this session.",
            ReportWith().Describe(failure));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("\r\nsecond line")]
    public void Describe_WithoutAUsableMessage_FallsBack(string? message)
    {
        Exception? failure = message == null ? null : new InvalidOperationException(message);

        Assert.Equal(PatchFailureReport.Fallback, ReportWith().Describe(failure!));
    }

    [Fact]
    public void Describe_LongText_IsCapped()
    {
        string longPath = @"D:\" + new string('a', 400) + @"\System.Collections.Immutable.dll";
        var report = ReportWith(new FakeAssembly(Immutable, "1.2.5.0", longPath));

        string text = report.Describe(ImmutableTypeLoad);

        Assert.Equal(PatchFailureReport.MaxPopupLength, text.Length);
        Assert.EndsWith("...", text);
    }

    [Fact]
    public void Describe_WhenAssembliesCannotBeListed_FallsBack()
    {
        var report = new PatchFailureReport(() => throw new InvalidOperationException("unavailable"));

        Assert.Equal(PatchFailureReport.Fallback, report.Describe(ImmutableTypeLoad));
    }

    [Fact]
    public void ListLoadedCopies_ListsEveryCopyWithItsVersionAndLocation()
    {
        var report = ReportWith(
            new FakeAssembly(Immutable, "1.2.5.0", StrayPath),
            new FakeAssembly("Coop.Core", "1.0.0.0", @"D:\Coop.Core.dll"),
            new FakeAssembly(Immutable, "9.0.0.0", CoopPath));

        Assert.Equal(
            $"Loaded copies of {Immutable}: 1.2.5.0 at {StrayPath}; 9.0.0.0 at {CoopPath}",
            report.ListLoadedCopies(ImmutableTypeLoad));
    }

    [Fact]
    public void ListLoadedCopies_SkipsDynamicAndUnreadableAssemblies()
    {
        var report = ReportWith(
            new FakeAssembly(Immutable, "1.2.5.0", location: string.Empty, isDynamic: true),
            new UnreadableAssembly(),
            new FakeAssembly(Immutable, "8.0.0.0", location: string.Empty));

        Assert.Equal(
            $"Loaded copies of {Immutable}: 8.0.0.0 in memory",
            report.ListLoadedCopies(ImmutableTypeLoad));
    }

    [Fact]
    public void ListLoadedCopies_WhenNoCopyIsLoaded_SaysSo()
    {
        Assert.Equal($"No copy of {Immutable} is loaded", ReportWith().ListLoadedCopies(ImmutableTypeLoad));
    }

    [Fact]
    public void ListLoadedCopies_ForAFailureThatIsNotALoad_IsEmpty()
    {
        Assert.Empty(ReportWith().ListLoadedCopies(new InvalidOperationException("not a load")));
    }

    private sealed class FakeAssembly : Assembly
    {
        private readonly string name;
        private readonly Version version;
        private readonly string location;
        private readonly bool isDynamic;

        public FakeAssembly(string name, string version, string location, bool isDynamic = false)
        {
            this.name = name;
            this.version = Version.Parse(version);
            this.location = location;
            this.isDynamic = isDynamic;
        }

        public override AssemblyName GetName() => new AssemblyName(name) { Version = version };

        public override bool IsDynamic => isDynamic;

        // Matches AssemblyBuilder, whose Location throws.
        public override string Location => isDynamic ? throw new NotSupportedException() : location;
    }

    private sealed class UnreadableAssembly : Assembly
    {
        public override AssemblyName GetName() => throw new FileLoadException("unreadable");
    }
}
