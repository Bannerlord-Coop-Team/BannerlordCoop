using Common;
using Common.Commands;
#if DEBUG
using Coop.Core.Common.Commands;
#endif
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
    [Theory]
    [MemberData(nameof(CorrectedCommandSides))]
    public void CorrectedCommands_ExposeExpectedSide(string outerTypeName, string typeName, CoopCommandSide expectedSide)
    {
        ICoopCommand command = CreateCommand(outerTypeName, typeName);

        Assert.Equal(expectedSide, command.Side);
    }

    [Theory]
    [InlineData(false, "HeroDeveloperCommands", "HeroDeveloperAddAttributePointsCoopCommand", "This command is only available on the server")]
    [InlineData(true, "UnstuckCommand", "UnstuckCoopCommand", "This command is only available on the client")]
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

    public static IEnumerable<object[]> CorrectedCommandSides()
    {
#if DEBUG
        yield return new object[] { "JoinDebugCommands", "JoinStateCoopCommand", CoopCommandSide.Both };
#endif
        yield return new object[] { "ArenaMasterCommands", "ViewArenaMasterInteractionsCommandCoopCommand", CoopCommandSide.Both };
        yield return new object[] { "CaravansCommands", "CaravansViewProhibitedKingdomsCoopCommand", CoopCommandSide.Both };
        yield return new object[] { "CaravansCommands", "CaravansViewInteractedCaravansCoopCommand", CoopCommandSide.Both };
        yield return new object[] { "CaravansCommands", "CaravansViewTakenTradeRumorsCoopCommand", CoopCommandSide.Both };
        yield return new object[] { "TradeSkillCommands", "InventoryViewPlayerTradeDataCoopCommand", CoopCommandSide.Both };
        yield return new object[] { "TradeSkillCommands", "InventoryViewPlayerTradeRumorsCoopCommand", CoopCommandSide.Both };
        yield return new object[] { "TradeSkillCommands", "InventoryViewEnteredSettlementsCoopCommand", CoopCommandSide.Both };
        yield return new object[] { "SmithingCommands", "CraftingCraftedItemHistoryCoopCommand", CoopCommandSide.Both };
        yield return new object[] { "SmithingCommands", "CraftingCraftingPiecesXpCoopCommand", CoopCommandSide.Both };
        yield return new object[] { "SmithingCommands", "CraftingUnlockedCraftingPiecesCoopCommand", CoopCommandSide.Both };
        yield return new object[] { "VillagerPartiesCommands", "ViewInteractedVillagersCommandCoopCommand", CoopCommandSide.Both };
        yield return new object[] { "WorkshopDebugCommand", "ViewWarehouseRostersCommandCoopCommand", CoopCommandSide.Both };
        yield return new object[] { "HeroDebugCommand", "HeroAddPowerCoopCommand", CoopCommandSide.Server };
        yield return new object[] { "HeroDebugCommand", "HeroSetGoldCoopCommand", CoopCommandSide.Server };
        yield return new object[] { "HeroDebugCommand", "HeroSetGoldStateCoopCommand", CoopCommandSide.Server };
        yield return new object[] { "HeroDebugCommand", "HeroSetHitpointsCoopCommand", CoopCommandSide.Server };
        yield return new object[] { "HeroDebugCommand", "HeroSetBannerItemCoopCommand", CoopCommandSide.Server };
        yield return new object[] { "HeroDebugCommand", "HeroSetIssueCoopCommand", CoopCommandSide.Server };
        yield return new object[] { "HeroDebugCommand", "HeroRefreshVolunteersCoopCommand", CoopCommandSide.Server };
        yield return new object[] { "IssuesDebugCommand", "IssuesGiveCoopCommand", CoopCommandSide.Server };
        yield return new object[] { "IssuesDebugCommand", "IssuesCompleteCoopCommand", CoopCommandSide.Server };
        yield return new object[] { "TacticalUnitSymbolsDebugCommand", "UiTacticalSymbolsCoopCommand", CoopCommandSide.Server };
        yield return new object[] { "PlayerCaptivityCommands", "PlayerCaptivityRandomCapturePlayerCoopCommand", CoopCommandSide.Server };
        yield return new object[] { "PlayerCaptivityCommands", "PlayerCaptivityCapturePlayerCoopCommand", CoopCommandSide.Server };
        yield return new object[] { "PlayerCaptivityCommands", "PlayerCaptivityCapturePlayerFixtureCoopCommand", CoopCommandSide.Server };
        yield return new object[] { "PlayerCaptivityCommands", "PlayerCaptivityRestoreRosterFixtureCoopCommand", CoopCommandSide.Server };
        yield return new object[] { "PlayerCaptivityCommands", "PlayerCaptivityReleasePlayerCoopCommand", CoopCommandSide.Server };
        yield return new object[] { "PlayerCaptivityCommands", "PlayerCaptivityPrepareVisualTestFixtureCoopCommand", CoopCommandSide.Server };
        yield return new object[] { "PlayerCaptivityCommands", "PlayerCaptivityRestoreVisualTestFixtureCoopCommand", CoopCommandSide.Server };
        yield return new object[] { "PlayerCaptivityCommands", "PlayerCaptivityRansomPlayerAtSettlementCoopCommand", CoopCommandSide.Server };
        yield return new object[] { "PlayerCaptivityCommands", "PlayerCaptivityRestorePartyFixtureStateCoopCommand", CoopCommandSide.Server };
#if DEBUG
        yield return new object[] { "AiLordPeaceReleaseFixtureCommands", "PlayerCaptivityCaptureAiLordFixtureCoopCommand", CoopCommandSide.Server };
        yield return new object[] { "LargeBattleRosterFixtureCommands", "LargeBattleRosterStatusCoopCommand", CoopCommandSide.Server };
        yield return new object[] { "LargeBattleRosterFixtureCommands", "ExactBattleRosterStatusCoopCommand", CoopCommandSide.Server };
        yield return new object[] { "LargeBattleRosterFixtureCommands", "LargeBattleRosterRestoreCoopCommand", CoopCommandSide.Server };
        yield return new object[] { "LargeBattleRosterFixtureCommands", "ExactBattleRosterRestoreCoopCommand", CoopCommandSide.Server };
#endif
        yield return new object[] { "PartyCommands", "RestorePositionCoopCommand", CoopCommandSide.Server };
#if DEBUG
        yield return new object[] { "MapEventDebugCommands", "LateJoinModeExitMissionsCoopCommand", CoopCommandSide.Server };
        yield return new object[] { "MapEventDebugCommands", "LateJoinModeCleanupCoopCommand", CoopCommandSide.Server };
#endif
        yield return new object[] { "MapEventDebugCommands", "LateJoinModeFixtureStateCoopCommand", CoopCommandSide.Server };
        yield return new object[] { "MapEventDebugCommands", "StartAttackMissionCoopCommand", CoopCommandSide.Client };
        yield return new object[] { "CampaignOptionsCommands", "CampaignOptionsIsIronmanModeCoopCommand", CoopCommandSide.Both };
    }

    private static ICoopCommand CreateCommand(string outerTypeName, string typeName)
    {
        Type commandType = GetCommandType(outerTypeName, typeName);
        return (ICoopCommand)Activator.CreateInstance(commandType)!;
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
