using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Template.Commands;

internal class SettlementCommands
{
    [CommandLineArgumentFunction("enter_random_castle", "coop.debug.settlements")]
    public static string TemplateCommand(List<string> strings)
    {
        var castles = Campaign.Current.CampaignObjectManager.Settlements.Where(settlement => settlement.IsCastle).ToArray();

        Random random = new Random();

        var randomCastle = castles[random.Next(castles.Length)];

        EncounterManager.StartSettlementEncounter(MobileParty.MainParty, randomCastle);

        return $"Entering {randomCastle.Name} castle";
    }
}
