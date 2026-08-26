using GameInterface.Services.Save.Patches;
using System;
using Xunit;

namespace GameInterface.Tests.Services.Save;

public class SaveManagerSaveDiagnosticsPatchTests
{
    [Fact]
    public void FormatErrorMessages_PreservesEveryVanillaSaveError()
    {
        string result = SaveManagerSaveDiagnosticsPatch.FormatErrorMessages(
            new[] { "definition failed", "serialization failed" });

        Assert.Equal("[0] definition failed | [1] serialization failed", result);
    }

    [Fact]
    public void FormatErrorMessages_ReportsMissingDetails()
    {
        Assert.Equal("<no details>", SaveManagerSaveDiagnosticsPatch.FormatErrorMessages(null));
        Assert.Equal("<no details>", SaveManagerSaveDiagnosticsPatch.FormatErrorMessages(Array.Empty<string>()));
    }
}
