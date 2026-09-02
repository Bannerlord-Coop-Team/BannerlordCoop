using Autofac;
using Common;
using Common.Commands;
using Common.Util;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Utils.Commands;

[Collection(global::GameInterface.Tests.ModInformationRoleCollection.Name)]
public class WorldSettlementDirectCommandTests
{
    private static readonly HashSet<string> OwningTypes = new HashSet<string>
    {
        "ArenaMasterCommands",
        "BesiegerCampDebugCommand",
        "LocationDebugCommand",
        "FollowPartyFixtureCommands",
        "MercenaryStockDebugCommand",
        "MobilePartyDebugCommand",
        "SettlementAuditorCommand",
        "SettlementCommands",
        "SiegeDebugCommand",
        "TournamentDebugCommand",
        "TownAuditorDebugCommand",
        "TownDebugCommand",
        "RaidDebugCommands",
        "VillageDebugCommand",
        "VillagerPartiesCommands",
        "WorkshopDebugCommand",
    };

    [Fact]
    public void MigratedCommands_ReplaceAllAttributedMethodsWithDirectCommands()
    {
        Type[] commandTypes = GetCommandTypes();

#if DEBUG
        Assert.Equal(134, commandTypes.Length);
#else
        Assert.Equal(123, commandTypes.Length);
#endif
        Assert.All(commandTypes, type =>
        {
            Assert.Equal(typeof(object), type.BaseType);
            Assert.Equal(new[] { typeof(ICoopCommand) }, type.GetInterfaces());
            Assert.EndsWith("CoopCommand", type.Name);
            Assert.Contains(type.DeclaringType.Name, OwningTypes);
        });

        MethodInfo[] attributedMethods = typeof(GameInterfaceModule).Assembly.GetTypes()
            .Where(type => OwningTypes.Contains(type.Name))
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.IsDefined(
                typeof(CommandLineFunctionality.CommandLineArgumentFunction), inherit: false))
            .ToArray();

        Assert.Empty(attributedMethods);
    }

    [Fact]
    public void MigratedCommands_HaveUniqueNormalizedMetadata()
    {
        ICoopCommand[] commands = CreateCommands();
        var registry = new CoopCommandRegistry(commands, new LoggerConfiguration().CreateLogger());

        Assert.Equal(commands.Length, registry.Commands.Count);
        Assert.Equal(
            commands.Length,
            commands.Select(command => $"{command.Prefix}.{command.Name}").Distinct().Count());
        Assert.All(commands, command =>
        {
            Assert.Matches("^coop(?:\\.[a-z0-9_]+)+$", command.Prefix);
            Assert.Matches("^[a-z0-9]+(?:_[a-z0-9]+)*$", command.Name);
            Assert.False(string.IsNullOrWhiteSpace(command.Description));
            Assert.NotNull(command.ExpectedArgs);
            Assert.All(command.ExpectedArgs, expectedArg =>
            {
                Assert.Matches(new Regex("^[a-z][A-Za-z0-9]*$"), expectedArg.Name);
                Assert.False(string.IsNullOrWhiteSpace(expectedArg.Description));
            });
        });
    }

    [Fact]
    public void Registry_RejectsInvalidArgumentCountBeforeCommandLogic()
    {
        ICoopCommand command = CreateCommand("coop.debug.besiegercamp.set_progress");
        var registry = new CoopCommandRegistry(
            new[] { command },
            new LoggerConfiguration().CreateLogger());

        CoopCommandResult result = registry.ProcessCommand(
            $"{command.Prefix}.{command.Name}",
            new TestArgs(Array.Empty<string>()));

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_arguments", result.ErrorCode);
        Assert.Contains("<besiegerCampId>", result.Output);
    }

    [Fact]
    public void TournamentClientRoleRejection_IsExplicitFailure()
    {
        bool originalIsServer = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = false;
            ICoopCommand command = CreateCommand("coop.debug.tournaments.add_tournament_to_town");

            CoopCommandResult result = command.ProcessCommand(new TestArgs(new[] { "Danustica" }));

            Assert.False(result.Succeeded);
            Assert.Equal("command_failed", result.ErrorCode);
        }
        finally
        {
            ModInformation.IsServer = originalIsServer;
        }
    }

    [Fact]
    public void GetTownName_RegisteredNonSettlement_IsExplicitFailure()
    {
        var objectManager = new ObjectManager(Serilog.Core.Logger.None);
        Assert.True(objectManager.AddExisting("registered-object", new object()));

        CoopCommandResult result = RunWithObjectManager(objectManager, () =>
            CreateCommand("coop.debug.settlements.get_town_name")
                .ProcessCommand(new TestArgs(new[] { "registered-object" })));

        Assert.False(result.Succeeded);
        Assert.Equal("command_failed", result.ErrorCode);
        Assert.Contains("was not of type Settlement", result.Output);
    }

    [Fact]
    public void StartRebellion_TownUnderSiege_IsExplicitFailure()
    {
        var objectManager = new ObjectManager(Serilog.Core.Logger.None);
        var clan = ObjectHelper.SkipConstructor<Clan>();
        var town = ObjectHelper.SkipConstructor<Town>();
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        var party = ObjectHelper.SkipConstructor<PartyBase>();
        town._ownerClan = clan;
        town._owner = party;
        settlement._name = new TextObject("Danustica");
        settlement.Party = party;
        settlement.Town = town;
        settlement.SiegeEvent = ObjectHelper.SkipConstructor<SiegeEvent>();
        party.Settlement = settlement;
        Assert.True(objectManager.AddExisting("town_comp_ES1", town));

        bool originalIsServer = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = true;
            CoopCommandResult result = RunWithObjectManager(objectManager, () =>
                CreateCommand("coop.debug.town.start_rebellion")
                    .ProcessCommand(new TestArgs(new[] { "town_comp_ES1" })));

            Assert.False(result.Succeeded);
            Assert.Equal("command_failed", result.ErrorCode);
            Assert.Contains("is under siege", result.Output);
        }
        finally
        {
            ModInformation.IsServer = originalIsServer;
        }
    }

#if DEBUG
    [Theory]
    [InlineData("coop.debug.tournaments.danustica_fixture_state")]
    [InlineData("coop.debug.tournaments.danustica_observe")]
    public void TournamentObservation_MissingDanusticaContext_IsExplicitFailure(string fullName)
    {
        Campaign previousCampaign = Campaign.Current;
        try
        {
            Campaign.Current = null;

            CoopCommandResult result = CreateCommand(fullName)
                .ProcessCommand(new TestArgs(Array.Empty<string>()));

            Assert.False(result.Succeeded);
            Assert.Equal("command_failed", result.ErrorCode);
            Assert.Contains("Unable to resolve Danustica", result.Output);
        }
        finally
        {
            Campaign.Current = previousCampaign;
        }
    }
#endif

    [Theory]
    [InlineData("coop.debug.workshop.set_workshop_custom_name")]
    [InlineData("coop.debug.workshop.set_workshop_owner")]
    public void WorkshopCommands_RequireSettlementStringId(string fullName)
    {
        ICoopCommand command = CreateCommand(fullName);
        IExpectedArgs settlementArgument = command.ExpectedArgs[0];

        Assert.Equal("settlementId", settlementArgument.Name);
        Assert.Equal("The settlement id.", settlementArgument.Description);
    }

    private static CoopCommandResult RunWithObjectManager(
        IObjectManager objectManager,
        Func<CoopCommandResult> invoke)
    {
        bool hadPreviousContainer = ContainerProvider.TryGetContainer(out var previousContainer);
        var builder = new ContainerBuilder();
        builder.RegisterInstance(objectManager).As<IObjectManager>();
        using var container = builder.Build();

        try
        {
            using (ContainerProvider.UseContainerThreadSafe(container))
            {
                return invoke();
            }
        }
        finally
        {
            if (hadPreviousContainer)
                ContainerProvider.SetContainer(previousContainer);
            else
                ContainerProvider.Clear();
        }
    }

    private static ICoopCommand CreateCommand(string fullName)
    {
        return Assert.Single(
            CreateCommands(),
            command => $"{command.Prefix}.{command.Name}" == fullName);
    }

    private static ICoopCommand[] CreateCommands()
    {
        return GetCommandTypes()
            .Select(type => (ICoopCommand)Activator.CreateInstance(type))
            .ToArray();
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
