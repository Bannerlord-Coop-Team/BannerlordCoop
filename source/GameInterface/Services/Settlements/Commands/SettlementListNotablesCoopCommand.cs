using Common.Commands;
using GameInterface.Services.ObjectManager;
using System;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Commands;

// coop.debug.settlements.list_notables
/// <summary>
/// Lists the notables of the settlement the local player is in, with the id that
/// coop.debug.issues.give takes and the name the game shows.
/// </summary>
public sealed class SettlementListNotablesCoopCommand : ICoopCommand
{
    private readonly IObjectManager objectManager;

    public SettlementListNotablesCoopCommand(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public string Prefix => "coop.debug.settlements";

    public string Name => "list_notables";

    public string Description => "Lists the notables in the settlement the client is in, with their ids and names.";

    public CoopCommandSide Side => CoopCommandSide.Client;

    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (Campaign.Current == null || MobileParty.MainParty == null)
        {
            return Failed("The campaign is not loaded");
        }

        var settlement = Settlement.CurrentSettlement;
        if (settlement == null)
        {
            return Failed("The main party is not in a settlement");
        }

        return new CoopCommandResult(true, Describe(settlement));
    }

    internal string Describe(Settlement settlement)
    {
        var notables = Hero.AllAliveHeroes
            .Where(hero => hero.IsNotable && hero.CurrentSettlement == settlement)
            .ToList();

        var output = new StringBuilder();
        output.AppendLine($"Settlement: {settlement.Name} ({settlement.StringId}), {notables.Count} notables");

        foreach (var hero in notables)
        {
            var id = objectManager.TryGetId(hero, out var registeredId) ? $"'{registeredId}'" : "not registered";
            var issue = hero.Issue?.GetType().Name ?? "none";

            output.AppendLine($"ID: {id}, Name: '{hero.Name}', Occupation: {hero.Occupation}, Issue: {issue}");
        }

        return output.ToString();
    }

    private static CoopCommandResult Failed(string output)
    {
        return new CoopCommandResult(false, output, "command_failed");
    }
}
