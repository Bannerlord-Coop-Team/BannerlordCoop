using Common;
using Common.Commands;
using GameInterface.Services.Party.Commands;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.Party;

/// <summary>
/// Added a regression test to check first if the instance is a server and
/// if that is true, to return the message.
/// </summary>
[Collection(ModInformationRoleCollection.Name)]
public class PartyCommandsTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;

    public void Dispose()
    {
        ModInformation.IsServer = wasServer;
    }

    [Fact]
    public void WhoAmI_WhenServer_ReturnsClientOnlyError()
    {
        ModInformation.IsServer = true;

        var command = new PartyCommands.WhoAmICoopCommand();
        CoopCommandResult result = command.ProcessCommand(
            new CoopCommandArgsFactory().FromValues(Array.Empty<string>()));

        Assert.False(result.Succeeded);
        Assert.Equal("Command can only be run on a client.", result.Output);
    }

    [Fact]
    public void MoveOffset_IssuesBehaviorAndNavigationMovement()
    {
        var method = AccessTools.Method(
            typeof(PartyCommands.MoveOffsetCoopCommand),
            nameof(PartyCommands.MoveOffsetCoopCommand.ProcessCommand));
        var instructions = PatchProcessor.GetOriginalInstructions(method);

        Assert.Contains(instructions, instruction => instruction.Calls(
            AccessTools.Method(typeof(MobileParty), "SetNavigationModePoint")));
        Assert.Contains(instructions, instruction => instruction.Calls(
            AccessTools.Method(typeof(MobileParty), nameof(MobileParty.SetMoveGoToPoint))));
    }
}
