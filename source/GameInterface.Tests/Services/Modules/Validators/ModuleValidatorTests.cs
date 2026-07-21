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
}
