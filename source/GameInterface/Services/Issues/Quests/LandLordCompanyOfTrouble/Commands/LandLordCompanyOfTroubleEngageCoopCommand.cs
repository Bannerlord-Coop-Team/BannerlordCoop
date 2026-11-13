using Common.Commands;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble.Commands;

internal sealed class LandLordCompanyOfTroubleEngageCoopCommand : ICoopCommand
{
    private readonly ILandLordCompanyOfTroubleService service;

    public LandLordCompanyOfTroubleEngageCoopCommand(ILandLordCompanyOfTroubleService service)
    {
        this.service = service;
    }

    public string Prefix => "coop.debug.issues.company_of_trouble";

    public string Name => "engage";

    public string Description => "Sends this player's party to attack the hostile company, as a click on it on the map does, so the player is the attacker in the encounter.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        var mainParty = MobileParty.MainParty;
        if (mainParty == null)
        {
            return Failed("This client has no main party");
        }

        var company = MobileParty.All.FirstOrDefault(party => party.IsActive && service.IsHostileCompany(party));
        if (company == null)
        {
            return Failed("No hostile company on the map");
        }

        mainParty.SetMoveEngageParty(company, MobileParty.NavigationType.Default);
        return new CoopCommandResult(true, $"{mainParty.Name} is moving to attack {company.Name} ({company.StringId}). Let time run.");
    }

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");
}
