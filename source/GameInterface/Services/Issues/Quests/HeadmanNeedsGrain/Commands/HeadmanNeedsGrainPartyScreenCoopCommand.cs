using Common.Commands;
using GameInterface.Utils.Commands;
using SandBox.GauntletUI;
using System.Linq;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain.Commands;

internal sealed class HeadmanNeedsGrainPartyScreenCoopCommand : ICoopCommand
{
    public string Prefix => "coop.debug.issues.grain";
    public string Name => "party_screen";
    public string Description => "Inspects the companion troop screen or drives its native transfer, Done and Cancel buttons.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } =
    {
        new ExpectedArgs("action", "status|move|done|cancel"),
        new ExpectedArgs("count", "Number of healthy regular troops to move to the mission.", false),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!(GameStateManager.Current?.ActiveState is PartyState state) ||
            state.PartyScreenMode != Helpers.PartyScreenHelper.PartyScreenMode.QuestTroopManage ||
            !(ScreenManager.TopScreen is GauntletPartyScreen screen) || screen._dataSource == null)
            return Failed("The companion troop screen is not open.");

        var vm = screen._dataSource;
        var logic = state.PartyScreenLogic;
        var action = args.Count > 0 ? args[0] : "status";
        switch (action)
        {
            case "status":
                break;
            case "move":
                if (args.Count < 2 || !int.TryParse(args[1], out var count) || count <= 0)
                    return Failed("move requires a positive troop count.");
                if (vm.IsEntireStackModifierActive || vm.IsFiveStackModifierActive)
                    return Failed("Release the stack transfer modifiers first.");
                for (var moved = 0; moved < count; moved++)
                {
                    var row = vm.MainPartyTroops.FirstOrDefault(candidate => candidate.Character != null &&
                        !candidate.Character.IsHero && candidate.Character.Tier >= 2 &&
                        candidate.Number > candidate.WoundedCount && candidate.IsTroopTransferrable);
                    if (row == null) return Failed($"Only {moved} eligible troops could be transferred.");
                    row.ExecuteTransferSingle();
                }
                break;
            case "done":
                if (!logic.IsDoneActive()) return Failed("Done is disabled until enough eligible troops are selected.");
                vm.ExecuteDone();
                return new CoopCommandResult(true, "Pressed the native Party screen Done button.");
            case "cancel":
                vm.ExecuteCancel();
                return new CoopCommandResult(true, "Pressed the native Party screen Cancel button.");
            default:
                return Failed("Use status, move, done or cancel.");
        }

        var left = logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Left].TotalManCount;
        var right = logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Right].TotalManCount;
        return new CoopCommandResult(true, $"left={left} right={right} done={logic.IsDoneActive()}");
    }

    private static CoopCommandResult Failed(string text) => new CoopCommandResult(false, text, "command_failed");
}
