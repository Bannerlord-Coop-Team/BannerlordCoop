using Common;
using Common.Commands;
#if DEBUG
using Coop.Core.Common.Commands;
#endif
using GameInterface.Tests.Utils.Commands;
using GameInterface;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services.Commands;

[Collection(global::GameInterface.Tests.ModInformationRoleCollection.Name)]
public class CoopCommandSideRegressionTests
{
    // One assembly scan for the whole table instead of one per row.
    [Fact]
    public void CorrectedCommands_ExposeExpectedSide()
    {
        ILookup<(string OuterTypeName, string TypeName), Type> nestedTypes = GetCommandAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.DeclaringType != null)
            .ToLookup(type => (type.DeclaringType!.Name, type.Name));

        Assert.All(CorrectedCommandSides(), expected =>
        {
            Type commandType = Assert.Single(nestedTypes[(expected.OuterTypeName, expected.TypeName)]);
            var command = CommandTestFactory.Create<ICoopCommand>(commandType);

            Assert.True(
                expected.Side == command.Side,
                $"{expected.OuterTypeName}.{expected.TypeName} exposes {command.Side}, expected {expected.Side}");
        });
    }

    [Theory]
    [InlineData(false, "HeroDeveloperCommands", "HeroDeveloperAddAttributePointsCoopCommand", "This command is only available on the server")]
    [InlineData(true, "MapEventDebugCommands", "StartAttackMissionCoopCommand", "This command is only available on the client")]
    public void Registry_RejectsWrongSideForRealCommands(
        bool isServer,
        string outerTypeName,
        string typeName,
        string expectedOutput)
    {
        bool originalIsServer = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = isServer;
            ICoopCommand command = CreateCommand(outerTypeName, typeName);
            var registry = new CoopCommandRegistry(new[] { command }, new LoggerConfiguration().CreateLogger());
            ICoopCommandArgs args = new CoopCommandArgsFactory().FromValues(Array.Empty<string>());

            CoopCommandResult result = registry.ProcessCommand($"{command.Prefix}.{command.Name}", args);

            Assert.False(result.Succeeded);
            Assert.Equal("command_wrong_side", result.ErrorCode);
            Assert.Equal(expectedOutput, result.Output);
        }
        finally
        {
            ModInformation.IsServer = originalIsServer;
        }
    }

    private static IEnumerable<(string OuterTypeName, string TypeName, CoopCommandSide Side)> CorrectedCommandSides()
    {
#if DEBUG
        yield return ("JoinDebugCommands", "JoinStateCoopCommand", CoopCommandSide.Both);
#endif
        yield return ("ArenaMasterCommands", "ViewArenaMasterInteractionsCommandCoopCommand", CoopCommandSide.Both);
        yield return ("CaravansCommands", "CaravansViewProhibitedKingdomsCoopCommand", CoopCommandSide.Both);
        yield return ("CaravansCommands", "CaravansViewInteractedCaravansCoopCommand", CoopCommandSide.Both);
        yield return ("CaravansCommands", "CaravansViewTakenTradeRumorsCoopCommand", CoopCommandSide.Both);
        yield return ("TradeSkillCommands", "InventoryViewPlayerTradeDataCoopCommand", CoopCommandSide.Both);
        yield return ("TradeSkillCommands", "InventoryViewPlayerTradeRumorsCoopCommand", CoopCommandSide.Both);
        yield return ("TradeSkillCommands", "InventoryViewEnteredSettlementsCoopCommand", CoopCommandSide.Both);
        yield return ("SmithingCommands", "CraftingCraftedItemHistoryCoopCommand", CoopCommandSide.Both);
        yield return ("SmithingCommands", "CraftingCraftingPiecesXpCoopCommand", CoopCommandSide.Both);
        yield return ("SmithingCommands", "CraftingUnlockedCraftingPiecesCoopCommand", CoopCommandSide.Both);
        yield return ("VillagerPartiesCommands", "ViewInteractedVillagersCommandCoopCommand", CoopCommandSide.Both);
        yield return ("WorkshopDebugCommand", "ViewWarehouseRostersCommandCoopCommand", CoopCommandSide.Both);
        yield return ("HeroDebugCommand", "HeroAddPowerCoopCommand", CoopCommandSide.Server);
        yield return ("HeroDebugCommand", "HeroSetGoldCoopCommand", CoopCommandSide.Server);
        yield return ("HeroDebugCommand", "HeroSetGoldStateCoopCommand", CoopCommandSide.Server);
        yield return ("HeroDebugCommand", "HeroSetHitpointsCoopCommand", CoopCommandSide.Server);
        yield return ("HeroDebugCommand", "HeroSetBannerItemCoopCommand", CoopCommandSide.Server);
        yield return ("HeroDebugCommand", "HeroSetIssueCoopCommand", CoopCommandSide.Server);
        yield return ("HeroDebugCommand", "HeroRefreshVolunteersCoopCommand", CoopCommandSide.Server);
        yield return ("TacticalUnitSymbolsDebugCommand", "UiTacticalSymbolsCoopCommand", CoopCommandSide.Server);
        yield return ("PlayerCaptivityCommands", "PlayerCaptivityRandomCapturePlayerCoopCommand", CoopCommandSide.Server);
        yield return ("PlayerCaptivityCommands", "PlayerCaptivityCapturePlayerCoopCommand", CoopCommandSide.Server);
        yield return ("PlayerCaptivityCommands", "PlayerCaptivityCapturePlayerFixtureCoopCommand", CoopCommandSide.Server);
        yield return ("PlayerCaptivityCommands", "PlayerCaptivityRestoreRosterFixtureCoopCommand", CoopCommandSide.Server);
        yield return ("PlayerCaptivityCommands", "PlayerCaptivityReleasePlayerCoopCommand", CoopCommandSide.Server);
        yield return ("PlayerCaptivityCommands", "PlayerCaptivityPrepareVisualTestFixtureCoopCommand", CoopCommandSide.Server);
        yield return ("PlayerCaptivityCommands", "PlayerCaptivityRestoreVisualTestFixtureCoopCommand", CoopCommandSide.Server);
        yield return ("PlayerCaptivityCommands", "PlayerCaptivityRansomPlayerAtSettlementCoopCommand", CoopCommandSide.Server);
        yield return ("PlayerCaptivityCommands", "PlayerCaptivityRestorePartyFixtureStateCoopCommand", CoopCommandSide.Server);
#if DEBUG
        yield return ("AiLordPeaceReleaseFixtureCommands", "PlayerCaptivityCaptureAiLordFixtureCoopCommand", CoopCommandSide.Server);
        yield return ("LargeBattleRosterFixtureCommands", "LargeBattleRosterStatusCoopCommand", CoopCommandSide.Server);
        yield return ("LargeBattleRosterFixtureCommands", "ExactBattleRosterStatusCoopCommand", CoopCommandSide.Server);
        yield return ("LargeBattleRosterFixtureCommands", "LargeBattleRosterRestoreCoopCommand", CoopCommandSide.Server);
        yield return ("LargeBattleRosterFixtureCommands", "ExactBattleRosterRestoreCoopCommand", CoopCommandSide.Server);
#endif
        yield return ("PartyCommands", "RestorePositionCoopCommand", CoopCommandSide.Server);
#if DEBUG
        yield return ("MapEventDebugCommands", "LateJoinModeExitMissionsCoopCommand", CoopCommandSide.Server);
        yield return ("MapEventDebugCommands", "LateJoinModeCleanupCoopCommand", CoopCommandSide.Server);
#endif
        yield return ("MapEventDebugCommands", "LateJoinModeFixtureStateCoopCommand", CoopCommandSide.Server);
        yield return ("MapEventDebugCommands", "StartAttackMissionCoopCommand", CoopCommandSide.Client);
        yield return ("CampaignOptionsCommands", "CampaignOptionsIsIronmanModeCoopCommand", CoopCommandSide.Both);
    }

    private static ICoopCommand CreateCommand(string outerTypeName, string typeName)
    {
        Type commandType = GetCommandType(outerTypeName, typeName);
        return CommandTestFactory.Create<ICoopCommand>(commandType);
    }

    private static Type GetCommandType(string outerTypeName, string typeName)
    {
        return GetCommandAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Single(type => type.Name == typeName && type.DeclaringType != null && type.DeclaringType.Name == outerTypeName);
    }

    private static IEnumerable<System.Reflection.Assembly> GetCommandAssemblies()
    {
        yield return typeof(GameInterfaceModule).Assembly;
#if DEBUG
        yield return typeof(JoinDebugCommands).Assembly;
#endif
    }
}
