using Common;
using GameInterface.Services.Modules;
using System;
using Xunit;

namespace GameInterface.Tests.Services.Modules;

/// <summary>
/// Covers refusing to host a save whose NavalDLC status differs from the War Sails decision.
/// </summary>
[Collection(ModInformationRoleCollection.Name)]
public class NavalSaveCompatibilityTests : IDisposable
{
    private static readonly string[] VanillaSave = { "Native", "SandBoxCore", "Sandbox" };
    private static readonly string[] NavalSave = { "Native", "SandBoxCore", "Sandbox", ModInformation.NavalDlcModuleId };

    private readonly bool previousNavalDlc = ModInformation.IsNavalDlcActive;

    // Puts back the process-wide flag.
    public void Dispose()
    {
        ModInformation.IsNavalDlcActive = previousNavalDlc;
    }

    [Fact]
    public void GetHostRefusal_AllowsAMatchingSave()
    {
        ModInformation.IsNavalDlcActive = true;
        Assert.Null(new NavalSaveCompatibility().GetHostRefusal("naval", NavalSave));

        ModInformation.IsNavalDlcActive = false;
        Assert.Null(new NavalSaveCompatibility().GetHostRefusal("vanilla", VanillaSave));
    }

    [Fact]
    public void GetHostRefusal_RefusesASaveWithoutNavalDlc_WhenWarSailsIsOn()
    {
        ModInformation.IsNavalDlcActive = true;

        string refusal = new NavalSaveCompatibility().GetHostRefusal("vanilla", VanillaSave);

        Assert.Contains("'vanilla' was made without NavalDLC", refusal);
    }

    [Fact]
    public void GetHostRefusal_RefusesASaveWithNavalDlc_WhenWarSailsIsOff()
    {
        ModInformation.IsNavalDlcActive = false;

        string refusal = new NavalSaveCompatibility().GetHostRefusal("naval", NavalSave);

        Assert.Contains("'naval' was made with NavalDLC", refusal);
    }
}
