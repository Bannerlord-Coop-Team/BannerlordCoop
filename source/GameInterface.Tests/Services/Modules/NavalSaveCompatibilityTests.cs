using Common;
using GameInterface.Services.Modules;
using Xunit;

namespace GameInterface.Tests.Services.Modules;

/// <summary>
/// Covers refusing to host a save whose NavalDLC status differs from the War Sails decision.
/// </summary>
public class NavalSaveCompatibilityTests
{
    private static readonly string[] VanillaSave = { "Native", "SandBoxCore", "Sandbox" };
    private static readonly string[] NavalSave = { "Native", "SandBoxCore", "Sandbox", ModInformation.NavalDlcModuleId };

    [Fact]
    public void GetHostRefusal_AllowsAMatchingSave()
    {
        Assert.Null(new NavalSaveCompatibility(() => true).GetHostRefusal("naval", NavalSave));
        Assert.Null(new NavalSaveCompatibility(() => false).GetHostRefusal("vanilla", VanillaSave));
    }

    [Fact]
    public void GetHostRefusal_RefusesASaveWithoutNavalDlc_WhenWarSailsIsOn()
    {
        string refusal = new NavalSaveCompatibility(() => true).GetHostRefusal("vanilla", VanillaSave);

        Assert.Contains("'vanilla' was made without NavalDLC", refusal);
    }

    [Fact]
    public void GetHostRefusal_RefusesASaveWithNavalDlc_WhenWarSailsIsOff()
    {
        string refusal = new NavalSaveCompatibility(() => false).GetHostRefusal("naval", NavalSave);

        Assert.Contains("'naval' was made with NavalDLC", refusal);
    }
}
