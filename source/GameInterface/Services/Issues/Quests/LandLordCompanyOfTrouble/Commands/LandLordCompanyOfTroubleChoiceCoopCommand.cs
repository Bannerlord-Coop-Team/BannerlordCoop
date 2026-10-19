using Common.Commands;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using Issue = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble.Commands;

internal sealed class LandLordCompanyOfTroubleChoiceCoopCommand : ICoopCommand
{
    private readonly IObjectManager objectManager;

    public LandLordCompanyOfTroubleChoiceCoopCommand(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public string Prefix => "coop.debug.issues.company_of_trouble";

    public string Name => "choose";

    public string Description => "Runs the consequence of one of the quest's dialog choices on this client, as if the player had picked it: accept, sell, leave, fight or steal.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = {
        new ExpectedArgs("choice", "accept (Rhagaea's offer), sell (a lord buys the contract), leave (\"Just leave\"), fight (\"So be it!\") or steal (the daily food theft)."),
    };

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (!objectManager.TryGetObject<Hero>(LandLordCompanyOfTroubleSetupCoopCommand.RhagaeaId, out var lord) || !(lord.Issue is Issue issue))
        {
            return Failed("Rhagaea has no Company of Trouble issue on this client");
        }

        if (args[0] == "accept")
        {
            if (issue.IssueQuest == null)
            {
                issue.StartIssueWithQuest();
            }

            ((Quest)issue.IssueQuest).QuestAcceptedConsequences();
            return Succeeded("Accepted Rhagaea's offer");
        }

        if (!(issue.IssueQuest is Quest quest) || !quest.IsOngoing)
        {
            return Failed("This client has no running Company of Trouble quest");
        }

        switch (args[0])
        {
            case "sell":
                quest.persuasion_complete_with_company_of_trouble_on_consequence();
                return Succeeded("Sold the contract");
            case "leave":
                quest._companyLeftQuestWillFail = true;
                GameMenu.ActivateGameMenu("company_of_trouble_menu");
                return Succeeded("Let the mercenaries leave");
            case "fight":
                quest.CreateCompanyEnemyParty();
                return Succeeded("The mercenaries turned hostile");
            case "steal":
                var food = DescribeFood();
                if (food == null)
                {
                    return Failed("No food to steal");
                }

                quest.TryToStealItemFromPlayer();
                return Succeeded($"Stole one {food} before the theft");
            default:
                return Failed($"Unknown choice '{args[0]}'");
        }
    }

    private static string DescribeFood()
    {
        var roster = MobileParty.MainParty.ItemRoster;

        for (int i = 0; i < roster.Count; i++)
        {
            var element = roster[i];

            if (!element.IsEmpty && element.EquipmentElement.Item.IsFood)
            {
                return $"{element.EquipmentElement.Item.StringId}, {element.Amount} in roster";
            }
        }

        return null;
    }

    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");
}
