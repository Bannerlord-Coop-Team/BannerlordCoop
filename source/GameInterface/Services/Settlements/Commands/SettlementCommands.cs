using Autofac;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Template.Commands;

internal class SettlementCommands
{
    [CommandLineArgumentFunction("enter_random_castle", "coop.debug.settlements")]
    public static string EnterRandomCastle(List<string> strings)
    {
        var castles = Campaign.Current.CampaignObjectManager.Settlements.Where(settlement => settlement.IsCastle).ToArray();

        Random random = new Random();

        var randomCastle = castles[random.Next(castles.Length)];

        EncounterManager.StartSettlementEncounter(MobileParty.MainParty, randomCastle);

        return $"Entering {randomCastle.Name} castle";
    }

    [CommandLineArgumentFunction("get_town_name", "coop.debug.settlements")]
    public static string GetTownName(List<string> strings)
    {
        if (strings.Count != 1) return "Invalid usage, expected \"get_town_name <settlment id>\"";

        string settlementId = strings.Single();

        if (ContainerProvider.TryGetContainer(out var container) == false) return "Unable to get town name";

        var objectManager = container.Resolve<IObjectManager>();

        if (objectManager.Contains(settlementId) == false) return $"{settlementId} does not exist";

        if (objectManager.TryGetObject<Settlement>(settlementId, out var settlement) == false)
            return $"{settlementId} was in object manager but was not of type Settlement";

        return $"Settlement Name: {settlement.Name}";
    }
}
