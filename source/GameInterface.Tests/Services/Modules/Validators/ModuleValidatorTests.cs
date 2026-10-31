using System;
using GameInterface.Services.Modules;
using GameInterface.Services.Modules.Validators;
using System.Collections.Generic;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Modules.Validators;

public class ModuleValidatorTests
{
    [Fact]
    public void Validate_ReturnsNull_WhenClientModulesMatchServerModules()
    {
        // Arrange
        var serverModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "ModuleA", Version = new ApplicationVersion(ApplicationVersionType.Release,1, 0, 0, 352) },
            new ModuleInfo { Id = "ModuleB", Version = new ApplicationVersion(ApplicationVersionType.Release,1, 2, 0, 352) }
        };

        var clientModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "ModuleA", Version = new ApplicationVersion(ApplicationVersionType.Release,1, 0, 0, 352) },
            new ModuleInfo { Id = "ModuleB", Version = new ApplicationVersion(ApplicationVersionType.Release,1, 2, 0, 352) }
        };

        var validator = new ModuleValidator();

        // Act
        var result = validator.Validate(serverModules, clientModules, out var error);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Validate_ReturnsErrorMessage_WhenClientIsMissingRequiredModule()
    {
        // Arrange
        var serverModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "ModuleA", Version = new ApplicationVersion(ApplicationVersionType.Release,1, 0, 0, 352) },
            new ModuleInfo { Id = "ModuleB", Version = new ApplicationVersion(ApplicationVersionType.Release,1, 2, 0, 352) }
        };

        var clientModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "ModuleA", Version = new ApplicationVersion(ApplicationVersionType.Release,1, 0, 0, 352) }
        };

        var validator = new ModuleValidator();

        // Act
        var result = validator.Validate(serverModules, clientModules, out var error);

        // Assert
        Assert.False(result);
        Assert.Equal("To join the server the module 'ModuleB' with version 'v1.2.0.352' is required.", error);
    }

    [Fact]
    public void Validate_ReturnsErrorMessage_WhenClientHasWrongVersion()
    {
        // Arrange
        var serverModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "ModuleA", Version = new ApplicationVersion(ApplicationVersionType.Release,1, 0, 0, 352) },
            new ModuleInfo { Id = "ModuleB", Version = new ApplicationVersion(ApplicationVersionType.Release,1, 2, 0, 352) }
        };

        var clientModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "ModuleA", Version = new ApplicationVersion(ApplicationVersionType.Release,1, 0, 0, 352) },
            new ModuleInfo { Id = "ModuleB", Version = new ApplicationVersion(ApplicationVersionType.Release,1, 1, 0, 352) }
        };

        var validator = new ModuleValidator();

        // Act
        var result = validator.Validate(serverModules, clientModules, out var error);

        // Assert
        Assert.False(result);
        Assert.Equal("Wrong version of module 'ModuleB' detected. Server uses 'v1.2.0.352', client uses 'v1.1.0.352'.", error);
    }

    [Fact]
    public void Validate_ReturnsErrorMessage_WhenGameVersionDiffers()
    {
        // Arrange
        var serverModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "Native", IsOfficial = true, Version = new ApplicationVersion(ApplicationVersionType.Release,1, 2, 0, 352) }
        };

        var clientModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "Native", IsOfficial = true, Version = new ApplicationVersion(ApplicationVersionType.Release,1, 1, 0, 352) }
        };

        var validator = new ModuleValidator();

        // Act
        var result = validator.Validate(serverModules, clientModules, out var error);

        // Assert
        Assert.False(result);
        Assert.Equal("Wrong game version detected. Server uses 'v1.2.0.352', client uses 'v1.1.0.352'.", error);
    }

    [Fact]
    public void Validate_AcceptsNavalDlc_WhenServerAndClientBothRunIt()
    {
        var serverModules = new List<ModuleInfo> { Native(), NavalDlc() };
        var clientModules = new List<ModuleInfo> { Native(), NavalDlc() };

        var result = NavalBuildValidator().Validate(serverModules, clientModules, out var error);

        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    public void Validate_AcceptsNavalDlc_WhenOnlyTheEngineChangeSetDiffers()
    {
        var dedicatedServerNaval = new ModuleInfo { Id = "NavalDLC", IsOfficial = true, IsDlc = true, Version = new ApplicationVersion(ApplicationVersionType.Release, 1, 2, 8, 118999) };
        var gameNaval = new ModuleInfo { Id = "NavalDLC", IsOfficial = true, IsDlc = true, Version = new ApplicationVersion(ApplicationVersionType.Release, 1, 2, 8, 119303) };

        var result = NavalBuildValidator().Validate(
            new List<ModuleInfo> { Native(), dedicatedServerNaval },
            new List<ModuleInfo> { Native(), gameNaval },
            out var error);

        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    public void Validate_RequiresNavalDlc_WhenOnlyTheServerRunsIt()
    {
        var serverModules = new List<ModuleInfo> { Native(), NavalDlc() };
        var clientModules = new List<ModuleInfo> { Native() };

        var result = NavalBuildValidator().Validate(serverModules, clientModules, out var error);

        Assert.False(result);
        Assert.Equal("To join the server the module 'NavalDLC' with version 'v1.0.0.352' is required.", error);
    }

    [Fact]
    public void Validate_RejectsNavalDlc_WhenTheBuildHasNoNavalCode()
    {
        var serverModules = new List<ModuleInfo> { Native(), NavalDlc() };
        var clientModules = new List<ModuleInfo> { Native(), NavalDlc() };

        var result = new ModuleValidator(() => false).Validate(serverModules, clientModules, out var error);

        Assert.False(result);
        Assert.Equal("DLC is not supported. Please disable the following module(s): 'NavalDLC'.", error);
    }

    [Fact]
    public void ValidateNoDlc_RejectsNavalDlc_WhenTheBuildHasNoNavalCode()
    {
        bool result = new ModuleValidator(() => false).ValidateNoDlc(new List<ModuleInfo> { Native(), NavalDlc() }, out var error);

        Assert.False(result);
        Assert.Equal("DLC is not supported. Please disable the following module(s): 'NavalDLC'.", error);
    }

    private static ModuleValidator NavalBuildValidator() => new ModuleValidator(() => true);

    private static ModuleInfo Native() =>
        new ModuleInfo { Id = "Native", IsOfficial = true, Version = new ApplicationVersion(ApplicationVersionType.Release, 1, 0, 0, 352) };

    private static ModuleInfo NavalDlc() =>
        new ModuleInfo { Id = "NavalDLC", IsOfficial = true, IsDlc = true, Version = new ApplicationVersion(ApplicationVersionType.Release, 1, 0, 0, 352) };

    [Fact]
    public void Validate_ReturnsErrorMessage_WhenClientHasDlcEnabled()
    {
        // Arrange
        var serverModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "Native", IsOfficial = true, Version = new ApplicationVersion(ApplicationVersionType.Release,1, 0, 0, 352) }
        };

        var clientModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "Native", IsOfficial = true, Version = new ApplicationVersion(ApplicationVersionType.Release,1, 0, 0, 352) },
            new ModuleInfo { Id = "SomeDlc", IsOfficial = true, IsDlc = true, Version = new ApplicationVersion(ApplicationVersionType.Release,1, 0, 0, 352) }
        };

        var validator = new ModuleValidator();

        // Act
        var result = validator.Validate(serverModules, clientModules, out var error);

        // Assert
        Assert.False(result);
        Assert.Equal("DLC is not supported. Please disable the following module(s): 'SomeDlc'.", error);
    }

    [Fact]
    public void ValidateNoDlc_ReturnsTrue_WhenNoDlcEnabled()
    {
        // Arrange
        var modules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "Native", IsOfficial = true, Version = new ApplicationVersion(ApplicationVersionType.Release,1, 0, 0, 352) },
            new ModuleInfo { Id = "ModuleA", Version = new ApplicationVersion(ApplicationVersionType.Release,1, 0, 0, 352) }
        };

        var validator = new ModuleValidator();

        // Act
        var result = validator.ValidateNoDlc(modules, out var error);

        // Assert
        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateNoDlc_ReturnsError_WhenDlcEnabled()
    {
        // Arrange
        var modules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "Native", IsOfficial = true, Version = new ApplicationVersion(ApplicationVersionType.Release,1, 0, 0, 352) },
            new ModuleInfo { Id = "DlcOne", IsOfficial = true, IsDlc = true, Version = new ApplicationVersion(ApplicationVersionType.Release,1, 0, 0, 352) },
            new ModuleInfo { Id = "DlcTwo", IsOfficial = true, IsDlc = true, Version = new ApplicationVersion(ApplicationVersionType.Release,1, 0, 0, 352) }
        };

        var validator = new ModuleValidator();

        // Act
        var result = validator.ValidateNoDlc(modules, out var error);

        // Assert
        Assert.False(result);
        Assert.Equal("DLC is not supported. Please disable the following module(s): 'DlcOne', 'DlcTwo'.", error);
    }

    [Fact]
    public void ValidateNoDlc_AllowsNavalDlc_AndRejectsAnyOtherDlc()
    {
        var validator = NavalBuildValidator();

        bool navalOnly = validator.ValidateNoDlc(new List<ModuleInfo> { Native(), NavalDlc() }, out var navalOnlyError);
        bool withOther = validator.ValidateNoDlc(new List<ModuleInfo>
        {
            NavalDlc(),
            new ModuleInfo { Id = "DlcOne", IsOfficial = true, IsDlc = true, Version = new ApplicationVersion(ApplicationVersionType.Release, 1, 0, 0, 352) },
        }, out var withOtherError);

        Assert.True(navalOnly);
        Assert.Null(navalOnlyError);
        Assert.False(withOther);
        Assert.Equal("DLC is not supported. Please disable the following module(s): 'DlcOne'.", withOtherError);
    }

    [Fact]
    public void Validate_ReturnsErrorMessage_WhenClientHasExtraModule()
    {
        // Arrange
        var serverModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "ModuleA", Version = new ApplicationVersion(ApplicationVersionType.Release,1, 0, 0, 352) },
            new ModuleInfo { Id = "ModuleB", Version = new ApplicationVersion(ApplicationVersionType.Release,1, 2, 0, 352) }
        };

        var clientModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "ModuleA", Version = new ApplicationVersion(ApplicationVersionType.Release,1, 0, 0, 352) },
            new ModuleInfo { Id = "ModuleB", Version = new ApplicationVersion(ApplicationVersionType.Release,1, 2, 0, 352) },
            new ModuleInfo { Id = "ModuleC", Version = new ApplicationVersion(ApplicationVersionType.Release,1, 0, 0, 352) }
        };

        var validator = new ModuleValidator();

        // Act
        var result = validator.Validate(serverModules, clientModules, out var error);

        // Assert
        Assert.False(result);
        Assert.Equal("Server does not support module 'ModuleC'.", error);
    }

    [Fact]
    public void Validate_ReportsEveryMismatch_WhenClientHasSeveralModuleProblems()
    {
        var serverModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "ModuleA", Version = new ApplicationVersion(ApplicationVersionType.Release, 1, 0, 0, 352) },
            new ModuleInfo { Id = "ModuleB", Version = new ApplicationVersion(ApplicationVersionType.Release, 1, 2, 0, 352) },
            new ModuleInfo { Id = "ModuleC", Version = new ApplicationVersion(ApplicationVersionType.Release, 2, 0, 0, 352) }
        };

        var clientModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "ModuleB", Version = new ApplicationVersion(ApplicationVersionType.Release, 1, 1, 0, 352) },
            new ModuleInfo { Id = "ModuleD", Version = new ApplicationVersion(ApplicationVersionType.Release, 1, 0, 0, 352) },
            new ModuleInfo { Id = "ModuleE", Version = new ApplicationVersion(ApplicationVersionType.Release, 1, 0, 0, 352) }
        };

        var validator = new ModuleValidator();

        var result = validator.Validate(serverModules, clientModules, out var error);

        Assert.False(result);
        Assert.Equal(string.Join(System.Environment.NewLine,
            "To join the server the module 'ModuleA' with version 'v1.0.0.352' is required.",
            "Wrong version of module 'ModuleB' detected. Server uses 'v1.2.0.352', client uses 'v1.1.0.352'.",
            "To join the server the module 'ModuleC' with version 'v2.0.0.352' is required.",
            "Server does not support module 'ModuleD'.",
            "Server does not support module 'ModuleE'."), error);
    }

    [Fact]
    public void Validate_KeepsLoneCoopReasonExact_WhenOnlyClientHasCoop()
    {
        // The client compares this reason exactly to join a server without Coop, so extra lines would break that.
        var version = new ApplicationVersion(ApplicationVersionType.Release, 1, 0, 0, 352);
        var serverModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "ModuleA", Version = version }
        };

        var clientModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "ModuleA", Version = version },
            new ModuleInfo { Id = "Coop", Version = version }
        };

        var validator = new ModuleValidator();

        var result = validator.Validate(serverModules, clientModules, out var error);

        Assert.False(result);
        Assert.Equal("Server does not support module 'Coop'.", error);
    }

    [Fact]
    public void Validate_Succeeds_WhenOnlyClientHasStoryMode()
    {
        var version = new ApplicationVersion(ApplicationVersionType.Release, 1, 2, 0, 352);
        var serverModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "Native", IsOfficial = true, Version = version }
        };
        var clientModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "Native", IsOfficial = true, Version = version },
            new ModuleInfo { Id = "StoryMode", IsOfficial = true, Version = version }
        };

        var validator = new ModuleValidator();

        var result = validator.Validate(serverModules, clientModules, out var error);

        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    public void Validate_Succeeds_WhenOnlyServerHasStoryMode()
    {
        var version = new ApplicationVersion(ApplicationVersionType.Release, 1, 2, 0, 352);
        var serverModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "Native", IsOfficial = true, Version = version },
            new ModuleInfo { Id = "StoryMode", IsOfficial = true, Version = version }
        };
        var clientModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "Native", IsOfficial = true, Version = version }
        };

        var validator = new ModuleValidator();

        var result = validator.Validate(serverModules, clientModules, out var error);

        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    public void Validate_Succeeds_WhenOnlyServerHasDedicatedServerWindowsModule()
    {
        var version = new ApplicationVersion(ApplicationVersionType.Release, 1, 4, 7, 352);
        var serverModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "Native", IsOfficial = true, Version = version },
            new ModuleInfo { Id = "DedicatedServer.Windows", Version = new ApplicationVersion(ApplicationVersionType.Release, 1, 4, 7, 0) }
        };
        var clientModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "Native", IsOfficial = true, Version = version }
        };

        var validator = new ModuleValidator();

        var result = validator.Validate(serverModules, clientModules, out var error);

        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    public void Validate_Succeeds_WhenOnlyServerHasDedicatedServerLinuxModule()
    {
        var version = new ApplicationVersion(ApplicationVersionType.Release, 1, 4, 7, 352);
        var serverModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "Native", IsOfficial = true, Version = version },
            new ModuleInfo { Id = "DedicatedServer.Linux", Version = new ApplicationVersion(ApplicationVersionType.Release, 1, 4, 7, 0) }
        };
        var clientModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "Native", IsOfficial = true, Version = version }
        };

        var validator = new ModuleValidator();

        var result = validator.Validate(serverModules, clientModules, out var error);

        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    public void Validate_Succeeds_WhenOnlyClientHasOfficialModule()
    {
        // A normal client launch enables official modules (CustomBattle, ...) a
        // dedicated server does not; they carry the game version and are tolerated.
        var version = new ApplicationVersion(ApplicationVersionType.Release, 1, 4, 7, 352);
        var serverModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "Native", IsOfficial = true, Version = version }
        };
        var clientModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "Native", IsOfficial = true, Version = version },
            new ModuleInfo { Id = "CustomBattle", IsOfficial = true, Version = version }
        };

        var validator = new ModuleValidator();

        var result = validator.Validate(serverModules, clientModules, out var error);

        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    public void Validate_Succeeds_WhenGameVersionDiffersOnlyByChangeset()
    {
        // A dedicated-server distribution and the Steam client are different
        // builds (changesets) of the same game version.
        var serverModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "Native", IsOfficial = true, Version = new ApplicationVersion(ApplicationVersionType.Release, 1, 4, 7, 117131) }
        };
        var clientModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "Native", IsOfficial = true, Version = new ApplicationVersion(ApplicationVersionType.Release, 1, 4, 7, 117484) }
        };

        var validator = new ModuleValidator();

        var result = validator.Validate(serverModules, clientModules, out var error);

        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    public void Validate_NamesModules_WhenDisplayNamesAreKnown()
    {
        var serverModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "CalradiaExpanded", Version = new ApplicationVersion(ApplicationVersionType.Release, 1, 2, 0, 0) },
            new ModuleInfo { Id = "RBM", Version = new ApplicationVersion(ApplicationVersionType.Release, 3, 0, 0, 0) }
        };

        var clientModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "CalradiaExpanded", Version = new ApplicationVersion(ApplicationVersionType.Release, 1, 1, 0, 0) },
            new ModuleInfo { Id = "ExtraMod", Version = new ApplicationVersion(ApplicationVersionType.Release, 1, 0, 0, 0) }
        };

        var names = new Dictionary<string, string>
        {
            ["CalradiaExpanded"] = "Calradia Expanded",
            ["RBM"] = "Realistic Battle Mod",
            ["ExtraMod"] = "Extra Mod",
        };

        var result = new ModuleValidator().Validate(serverModules, clientModules, out var error, names);

        Assert.False(result);
        Assert.Equal(string.Join(Environment.NewLine,
            "Wrong version of module 'Calradia Expanded' (CalradiaExpanded) detected. Server uses 'v1.2.0.0', client uses 'v1.1.0.0'.",
            "To join the server the module 'Realistic Battle Mod' (RBM) with version 'v3.0.0.0' is required.",
            "Server does not support module 'Extra Mod' (ExtraMod)."), error);
    }

    [Fact]
    public void Validate_UsesIdOnly_WhenNameMatchesIdOrIsUnknown()
    {
        var serverModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "ModuleA", Version = new ApplicationVersion(ApplicationVersionType.Release, 1, 0, 0, 0) },
            new ModuleInfo { Id = "ModuleB", Version = new ApplicationVersion(ApplicationVersionType.Release, 1, 0, 0, 0) }
        };

        var result = new ModuleValidator().Validate(serverModules, new List<ModuleInfo>(), out var error,
            new Dictionary<string, string> { ["ModuleA"] = "ModuleA" });

        Assert.False(result);
        Assert.Equal(string.Join(Environment.NewLine,
            "To join the server the module 'ModuleA' with version 'v1.0.0.0' is required.",
            "To join the server the module 'ModuleB' with version 'v1.0.0.0' is required."), error);
    }

    [Fact]
    public void Validate_KeepsUnsupportedCoopReasonExact_WhenCoopHasDisplayName()
    {
        var clientModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "Coop", Version = new ApplicationVersion(ApplicationVersionType.Release, 0, 1, 6, 0) }
        };

        var result = new ModuleValidator().Validate(new List<ModuleInfo>(), clientModules, out var error,
            new Dictionary<string, string> { ["Coop"] = "Bannerlord Coop" });

        Assert.False(result);
        Assert.Equal("Server does not support module 'Coop'.", error);
    }

    [Fact]
    public void Validate_NamesDlc_WhenDisplayNameIsKnown()
    {
        var gameVersion = new ApplicationVersion(ApplicationVersionType.Release, 1, 4, 8, 0);
        var serverModules = new List<ModuleInfo> { new ModuleInfo { Id = "Native", IsOfficial = true, Version = gameVersion } };
        var clientModules = new List<ModuleInfo>
        {
            new ModuleInfo { Id = "Native", IsOfficial = true, Version = gameVersion },
            new ModuleInfo { Id = "NavalDLC", IsOfficial = true, IsDlc = true, Version = gameVersion }
        };

        var result = new ModuleValidator().Validate(serverModules, clientModules, out var error,
            new Dictionary<string, string> { ["NavalDLC"] = "War Sails" });

        Assert.False(result);
        Assert.Equal("DLC is not supported. Please disable the following module(s): 'War Sails' (NavalDLC).", error);
    }

    [Fact]
    public void ModuleInfo_DisplayNameDoesNotTakePartInEquality()
    {
        // names live outside ModuleInfo so they never affect compatibility (#3885 review)
        Assert.Null(typeof(ModuleInfo).GetProperty("Name"));
    }
}