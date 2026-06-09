using System;
using System.Collections.Generic;
using GameInterface.Services.Modules;
using GameInterface.Services.Modules.Validators;
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
}