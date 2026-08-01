using GameInterface.Services.GameState;
using GameInterface.Services.Modules;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace GameInterface.Tests.Services.GameState;

public class ManagedServerLauncherTests
{
    [Fact]
    public void ResolveDedicatedServerExecutable_ReturnsPath_WhenBundledServerExists()
    {
        var moduleRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var executablePath = Path.Combine(
            moduleRoot,
            ManagedServerLauncher.DedicatedServerFolderName,
            ManagedServerLauncher.DedicatedServerExecutableName);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(executablePath)!);
            File.WriteAllText(executablePath, string.Empty);

            Assert.Equal(executablePath,
                ManagedServerLauncher.ResolveDedicatedServerExecutable(moduleRoot));
        }
        finally
        {
            Directory.Delete(moduleRoot, recursive: true);
        }
    }

    [Fact]
    public void ResolveDedicatedServerExecutable_ReturnsNull_WhenServerIsNotInstalled()
    {
        var moduleRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            // A module folder without a DedicatedServer deployment inside it.
            Directory.CreateDirectory(moduleRoot);

            Assert.Null(ManagedServerLauncher.ResolveDedicatedServerExecutable(moduleRoot));
        }
        finally
        {
            Directory.Delete(moduleRoot, recursive: true);
        }
    }

    [Fact]
    public void ResolveDedicatedServerExecutable_ReturnsNull_WithoutModuleRoot()
    {
        Assert.Null(ManagedServerLauncher.ResolveDedicatedServerExecutable(null));
        Assert.Null(ManagedServerLauncher.ResolveDedicatedServerExecutable(string.Empty));
    }

    [Fact]
    public void CanDedicatedServerHostModules_AcceptsTheStockModuleSet()
    {
        var modules = new List<ModuleInfo>
        {
            new ModuleInfo("Native", isOfficial: true, isDlc: false, default),
            new ModuleInfo("SandBoxCore", isOfficial: true, isDlc: false, default),
            new ModuleInfo("Sandbox", isOfficial: true, isDlc: false, default),
            new ModuleInfo("StoryMode", isOfficial: true, isDlc: false, default),
            new ModuleInfo("Coop", isOfficial: false, isDlc: false, default),
        };

        Assert.True(ManagedServerLauncher.CanDedicatedServerHostModules(modules));
    }

    [Fact]
    public void CanDedicatedServerHostModules_AcceptsTheDedicatedServerHostModules()
    {
        var modules = new List<ModuleInfo>
        {
            new ModuleInfo("Native", isOfficial: true, isDlc: false, default),
            new ModuleInfo("coop", isOfficial: false, isDlc: false, default),
            new ModuleInfo("DedicatedServer.Windows", isOfficial: false, isDlc: false, default),
        };

        Assert.True(ManagedServerLauncher.CanDedicatedServerHostModules(modules));
    }

    [Fact]
    public void CanDedicatedServerHostModules_RejectsCommunityModules()
    {
        var modules = new List<ModuleInfo>
        {
            new ModuleInfo("Native", isOfficial: true, isDlc: false, default),
            new ModuleInfo("Coop", isOfficial: false, isDlc: false, default),
            new ModuleInfo("SomeCommunityMod", isOfficial: false, isDlc: false, default),
        };

        Assert.False(ManagedServerLauncher.CanDedicatedServerHostModules(modules));
    }

    [Fact]
    public void CanDedicatedServerHostModules_RejectsDlcModules()
    {
        var modules = new List<ModuleInfo>
        {
            new ModuleInfo("Native", isOfficial: true, isDlc: false, default),
            new ModuleInfo("Coop", isOfficial: false, isDlc: false, default),
            new ModuleInfo("WarSails", isOfficial: true, isDlc: true, default),
        };

        Assert.False(ManagedServerLauncher.CanDedicatedServerHostModules(modules));
    }

    [Fact]
    public void CanDedicatedServerHostModules_RejectsNullCollection()
    {
        Assert.False(ManagedServerLauncher.CanDedicatedServerHostModules(null));
    }
}
