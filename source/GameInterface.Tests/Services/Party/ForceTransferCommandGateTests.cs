using GameInterface.Services.Party.Patches;
using GameInterface.Services.Villages;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.Party;

/// <summary>
/// The force volunteers screen must reject prisoner, upgrade, and execution
/// commands before Done, since the commit validation cannot honor them.
/// </summary>
public class ForceTransferCommandGateTests
{
    [Theory]
    [InlineData(PartyScreenLogic.PartyCommandCode.UpgradeTroop, PartyScreenLogic.TroopType.Member)]
    [InlineData(PartyScreenLogic.PartyCommandCode.RecruitTroop, PartyScreenLogic.TroopType.Prisoner)]
    [InlineData(PartyScreenLogic.PartyCommandCode.ExecuteTroop, PartyScreenLogic.TroopType.Prisoner)]
    [InlineData(PartyScreenLogic.PartyCommandCode.TransferPartyLeaderTroop, PartyScreenLogic.TroopType.Member)]
    [InlineData(PartyScreenLogic.PartyCommandCode.TransferTroop, PartyScreenLogic.TroopType.Prisoner)]
    [InlineData(PartyScreenLogic.PartyCommandCode.TransferTroopToLeaderSlot, PartyScreenLogic.TroopType.Prisoner)]
    [InlineData(PartyScreenLogic.PartyCommandCode.TransferAllTroops, PartyScreenLogic.TroopType.Prisoner)]
    [InlineData(PartyScreenLogic.PartyCommandCode.TransferTroop, PartyScreenLogic.TroopType.None)]
    public void BlockedCommands_AreFlagged(
        PartyScreenLogic.PartyCommandCode code,
        PartyScreenLogic.TroopType type)
    {
        Assert.True(PartyScreenLogicPatches.IsBlockedOnForceTransferScreen(code, type));
    }

    [Theory]
    [InlineData(PartyScreenLogic.PartyCommandCode.TransferTroop, PartyScreenLogic.TroopType.Member)]
    [InlineData(PartyScreenLogic.PartyCommandCode.TransferTroopToLeaderSlot, PartyScreenLogic.TroopType.Member)]
    [InlineData(PartyScreenLogic.PartyCommandCode.TransferAllTroops, PartyScreenLogic.TroopType.Member)]
    [InlineData(PartyScreenLogic.PartyCommandCode.ShiftTroop, PartyScreenLogic.TroopType.Member)]
    [InlineData(PartyScreenLogic.PartyCommandCode.ShiftTroop, PartyScreenLogic.TroopType.Prisoner)]
    [InlineData(PartyScreenLogic.PartyCommandCode.SortTroops, PartyScreenLogic.TroopType.Member)]
    public void AllowedCommands_AreNotFlagged(
        PartyScreenLogic.PartyCommandCode code,
        PartyScreenLogic.TroopType type)
    {
        Assert.False(PartyScreenLogicPatches.IsBlockedOnForceTransferScreen(code, type));
    }

    [Fact]
    public void ValidateCommandPrefix_PrisonerTransferBlockedWhileForceScreenOpen()
    {
        var roster = new object();
        ForceTransferScreenTracker.NoteLootScreenOpened("req-1", roster);
        try
        {
            var command = new PartyScreenLogic.PartyCommand();
            command.FillForTransferTroop(
                PartyScreenLogic.PartyRosterSide.Right,
                PartyScreenLogic.TroopType.Prisoner,
                null, 1, 0, 0);

            var result = false;
            Assert.False(PartyScreenLogicPatches.ValidateCommandPrefix(command, ref result));
            Assert.False(result);
        }
        finally
        {
            ForceTransferScreenTracker.Clear();
        }
    }

    [Fact]
    public void ValidateCommandPrefix_BulkPrisonerTransferBlockedWhileForceScreenOpen()
    {
        var roster = new object();
        ForceTransferScreenTracker.NoteLootScreenOpened("req-1", roster);
        try
        {
            var command = new PartyScreenLogic.PartyCommand();
            command.FillForTransferAllTroops(
                PartyScreenLogic.PartyRosterSide.Right,
                PartyScreenLogic.TroopType.Prisoner);

            var result = false;
            Assert.False(PartyScreenLogicPatches.ValidateCommandPrefix(command, ref result));
            Assert.False(result);
        }
        finally
        {
            ForceTransferScreenTracker.Clear();
        }
    }

    [Theory]
    [InlineData(PartyScreenLogic.PartyCommandCode.UpgradeTroop)]
    [InlineData(PartyScreenLogic.PartyCommandCode.RecruitTroop)]
    [InlineData(PartyScreenLogic.PartyCommandCode.ExecuteTroop)]
    public void ValidateCommandPrefix_SideEffectCommandsBlockedWhileForceScreenOpen(
        PartyScreenLogic.PartyCommandCode code)
    {
        var roster = new object();
        ForceTransferScreenTracker.NoteLootScreenOpened("req-1", roster);
        try
        {
            var command = new PartyScreenLogic.PartyCommand();
            FillCommand(command, code);

            var result = false;
            Assert.False(PartyScreenLogicPatches.ValidateCommandPrefix(command, ref result));
            Assert.False(result);
        }
        finally
        {
            ForceTransferScreenTracker.Clear();
        }
    }

    private static void FillCommand(PartyScreenLogic.PartyCommand command, PartyScreenLogic.PartyCommandCode code)
    {
        switch (code)
        {
            case PartyScreenLogic.PartyCommandCode.UpgradeTroop:
                command.FillForUpgradeTroop(
                    PartyScreenLogic.PartyRosterSide.Right,
                    PartyScreenLogic.TroopType.Member,
                    null, 1, 0, 0);
                break;
            case PartyScreenLogic.PartyCommandCode.RecruitTroop:
                command.FillForRecruitTroop(
                    PartyScreenLogic.PartyRosterSide.Right,
                    PartyScreenLogic.TroopType.Prisoner,
                    null, 1, 0);
                break;
            default:
                command.FillForExecuteTroop(
                    PartyScreenLogic.PartyRosterSide.Right,
                    PartyScreenLogic.TroopType.Prisoner,
                    null);
                break;
        }
    }

    [Fact]
    public void ValidateCommandPrefix_MemberTransferPassesThroughWhileForceScreenOpen()
    {
        var roster = new object();
        ForceTransferScreenTracker.NoteLootScreenOpened("req-1", roster);
        try
        {
            var command = new PartyScreenLogic.PartyCommand();
            command.FillForTransferTroop(
                PartyScreenLogic.PartyRosterSide.Left,
                PartyScreenLogic.TroopType.Member,
                null, 1, 0, 0);

            var result = false;
            Assert.True(PartyScreenLogicPatches.ValidateCommandPrefix(command, ref result));
        }
        finally
        {
            ForceTransferScreenTracker.Clear();
        }
    }

    [Fact]
    public void ValidateCommandPrefix_PrisonerTransferPassesThroughWithoutForceScreen()
    {
        ForceTransferScreenTracker.Clear();

        var command = new PartyScreenLogic.PartyCommand();
        command.FillForTransferTroop(
            PartyScreenLogic.PartyRosterSide.Right,
            PartyScreenLogic.TroopType.Prisoner,
            null, 1, 0, 0);

        var result = false;
        Assert.True(PartyScreenLogicPatches.ValidateCommandPrefix(command, ref result));
    }
}
