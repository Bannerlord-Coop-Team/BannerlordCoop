using Common;
using Common.Commands;
using GameInterface.Services.CampaignService.Commands;
using GameInterface.Services.GameDebug.Commands;
using GameInterface.Services.UI.Commands;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Utils.Commands;

[Collection(ModInformationRoleCollection.Name)]
public class SystemDeveloperDirectCommandTests
{
    private static readonly HashSet<string> OwningTypes = new HashSet<string>
    {
        "CampaignOptionsCommands",
        "ModOptionsCommands",
        "CaravansCommands",
        "CharacterDeveloperCommands",
        "CharacterObjectCommands",
        "CameraReset",
        "GameThreadDebugCommand",
        "PartySyncPerformanceLogsCommand",
        "UiDebugCommands",
        "UnstuckCommand",
        "HeroDeveloperCommands",
        "InventoryCommands",
        "TradeSkillCommands",
        "IssuesDebugCommand",
        "ItemObjectCommands",
        "ItemRosterDebugCommands",
        "AiLordPeaceReleaseFixtureCommands",
        "PlayerCaptivityCommands",
        "SaveDebugCommand",
        "SmithingCommands",
        "TemplateCommands",
        "TimeCommands",
        "SteamDebugCommand",
        "TacticalUnitSymbolsDebugCommand",
    };

    [Fact]
    public void MigratedCommands_AreDirectCommandsInTheirOwningTypes()
    {
        Type[] commandTypes = GetCommandTypes();

#if DEBUG
        Assert.Equal(103, commandTypes.Length);
#else
        Assert.Equal(92, commandTypes.Length);
#endif
        Assert.All(commandTypes, type =>
        {
            Assert.Equal(typeof(object), type.BaseType);
            Assert.Equal(new[] { typeof(ICoopCommand) }, type.GetInterfaces());
            Assert.EndsWith("CoopCommand", type.Name);
        });
    }

    [Fact]
    public void MigratedCommands_HaveUniqueNormalizedMetadata()
    {
        ICoopCommand[] commands = CreateCommands();
        var registry = new CoopCommandRegistry(commands, new LoggerConfiguration().CreateLogger());

        Assert.Equal(commands.Length, registry.Commands.Count);
        Assert.All(commands, command =>
        {
            Assert.Matches("^coop(?:\\.[a-z0-9_]+)*$", command.Prefix);
            Assert.Matches("^[a-z0-9]+(?:_[a-z0-9]+)*$", command.Name);
            Assert.False(string.IsNullOrWhiteSpace(command.Description));
            Assert.NotNull(command.ExpectedArgs);
        });
    }

    [Fact]
    public void ProcessLifetimeCommands_KeepAttributedEntryPoints()
    {
        AssertAttributed(typeof(SteamDebugCommand), nameof(SteamDebugCommand.Status));
        AssertAttributed(typeof(SteamDebugCommand), nameof(SteamDebugCommand.Join));
        AssertAttributed(typeof(BugReportLogSharingCommand), nameof(BugReportLogSharingCommand.Configure));

        string[] migratedSteamCommands = CreateCommands()
            .Where(command => command.GetType().DeclaringType == typeof(SteamDebugCommand))
            .Select(command => command.Name)
            .OrderBy(name => name)
            .ToArray();
        Assert.Equal(new[] { "host_lobby", "invite" }, migratedSteamCommands);
    }

    [Fact]
    public void ProcessCommand_ReadsCountOnlyForOptionalOrConditionalArguments()
    {
        string[] countReaders = GetCommandTypes()
            .Where(type => CallsArgumentCount(type.GetMethod(nameof(ICoopCommand.ProcessCommand))))
            .Select(type => ((ICoopCommand)Activator.CreateInstance(type)).Name)
            .OrderBy(name => name)
            .ToArray();

#if DEBUG
        Assert.Equal(
            new[]
            {
                "advance_time",
                "complete",
                "force_autosave",
                "instrument",
                "is_ironman_mode",
                "set_time_mode",
            },
            countReaders);
#else
        Assert.Equal(
            new[]
            {
                "advance_time",
                "complete",
                "force_autosave",
                "instrument",
                "is_ironman_mode",
            },
            countReaders);
#endif
    }

    [Fact]
    public void Registry_RejectsInvalidArgumentCountBeforeCommandLogic()
    {
        ICoopCommand command = Assert.Single(
            CreateCommands(),
            candidate => candidate.Name == "add_attribute_points");
        var registry = new CoopCommandRegistry(new[] { command }, new LoggerConfiguration().CreateLogger());

        CoopCommandResult result = registry.ProcessCommand(
            $"{command.Prefix}.{command.Name}",
            new TestArgs(new[] { "Hero", "With", "Spaces", "2" }));

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_arguments", result.ErrorCode);
    }

    [Fact]
    public void CampaignOptionClientRejection_IsExplicitFailure()
    {
        bool wasServer = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = false;
            ICoopCommand command = new CampaignOptionsCommands.CampaignOptionsAutoAllocateClanMemberPerksCoopCommand();

            CoopCommandResult result = command.ProcessCommand(new TestArgs(new[] { "true" }));

            Assert.False(result.Succeeded);
            Assert.Equal("command_failed", result.ErrorCode);
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    private static void AssertAttributed(Type owner, string methodName)
    {
        MethodInfo method = owner.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        Assert.True(method.IsDefined(
            typeof(CommandLineFunctionality.CommandLineArgumentFunction),
            inherit: false));
    }

    private static bool CallsArgumentCount(MethodInfo method)
    {
        byte[] il = method.GetMethodBody().GetILAsByteArray();
        for (int index = 0; index <= il.Length - sizeof(int); index++)
        {
            try
            {
                MethodBase referencedMethod = method.Module.ResolveMethod(BitConverter.ToInt32(il, index));
                if (referencedMethod.Name == "get_Count" &&
                    (referencedMethod.DeclaringType == typeof(ICoopCommandArgs) ||
                     referencedMethod.DeclaringType == typeof(IReadOnlyCollection<string>)))
                {
                    return true;
                }
            }
            catch (ArgumentException)
            {
            }
        }

        return false;
    }

    private static Type[] GetCommandTypes()
    {
        return typeof(GameInterfaceModule).Assembly.GetTypes()
            .Where(type => type.IsClass &&
                           !type.IsAbstract &&
                           type.DeclaringType != null &&
                           OwningTypes.Contains(type.DeclaringType.Name) &&
                           typeof(ICoopCommand).IsAssignableFrom(type))
            .ToArray();
    }

    private static ICoopCommand[] CreateCommands()
    {
        return GetCommandTypes()
            .Select(type => (ICoopCommand)Activator.CreateInstance(type))
            .ToArray();
    }

    private sealed class TestArgs : ICoopCommandArgs
    {
        private readonly IReadOnlyList<string> values;

        public TestArgs(IReadOnlyList<string> values)
        {
            this.values = values;
        }

        public int Count => values.Count;

        public string this[int index] => values[index];

        public IEnumerator<string> GetEnumerator() => values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
