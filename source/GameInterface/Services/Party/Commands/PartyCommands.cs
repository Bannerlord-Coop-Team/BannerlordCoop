using Autofac;
using Common;
using Common.Logging;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Party.Commands;

internal class PartyCommands
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyCommands>();

    /// <summary>
    /// Attempts to get the ObjectManager
    /// </summary>
    private static bool TryGetObjectManager(out IObjectManager objectManager)
    {
        objectManager = null;
        if (ContainerProvider.TryGetContainer(out var container) == false) return false;

        return container.TryResolve(out objectManager);
    }

    /// <summary>
    /// View character ids in a hero's party
    /// </summary>
    [CommandLineArgumentFunction("characterids", "coop.debug.mobileparty")]
    public static string ViewItemIdsCommand(List<string> strings)
    {
        if (strings.Count == 0) return "Hero name argument required.";

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var hero in Hero.AllAliveHeroes)
        {
            if (hero.Name.ToString() == strings[0])
            {
                stringBuilder.AppendLine("##" + hero.Name.ToString());
                stringBuilder.AppendLine("Member roster:");
                foreach (var rosterElement in hero.PartyBelongedTo.MemberRoster.data)
                {
                    stringBuilder.AppendLine(rosterElement.Character?.StringId + ": " + rosterElement.Number + " " + rosterElement.Xp);
                }

                stringBuilder.AppendLine("Prisoner roster:");
                foreach (var rosterElement in hero.PartyBelongedTo.PrisonRoster.data)
                {
                    stringBuilder.AppendLine(rosterElement.Character?.StringId + ": " + rosterElement.Number + " " + rosterElement.Xp);
                }
            }
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Hero not found.";
    }

    /// <summary>
    /// Add xp to all troops in a hero's party
    /// </summary>
    [CommandLineArgumentFunction("addtroopxp", "coop.debug.mobileparty")]
    public static string AddTroopXpCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";

        if (strings.Count != 2) return "Hero name and xp amount required.";

        if (!int.TryParse(strings[1], out int xpGain)) return "Please enter an integer for xp amount";

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var hero in Hero.AllAliveHeroes)
        {
            if (hero.Name.ToString() == strings[0])
            {
                var memberRoster = hero.PartyBelongedTo.MemberRoster;
                foreach (var troop in memberRoster.data)
                {
                    memberRoster.AddXpToTroop(troop.Character, xpGain);
                }

                stringBuilder.AppendLine("The party of " + hero.Name.ToString() + "got some xp.");
            }
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Hero not found.";
    }

    /// <summary>
    /// Add troops to a hero's party
    /// </summary>
    [CommandLineArgumentFunction("addtroops", "coop.debug.mobileparty")]
    public static string AddRecruitsCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";

        if (strings.Count == 0) return "Hero name required";

        if (TryGetObjectManager(out var objectManager) == false) return "Unable to resolve ObjectManager.";

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var hero in Hero.AllAliveHeroes)
        {
            if (hero.Name.ToString() == strings[0])
            {
                var memberRoster = hero.PartyBelongedTo.MemberRoster;
                var troopsToAdd = new Dictionary<string, int>()
                {
                    { "imperial_vigla_recruit", 5 },
                    { "imperial_recruit", 2 },
                    { "imperial_equite", 2 },
                    { "imperial_heavy_horseman", 2 }
                };

                foreach (var troopId in troopsToAdd.Keys)
                {
                    if (!objectManager.TryGetObject(troopId, out CharacterObject characterObject))
                    {
                        stringBuilder.AppendLine("Failed to retrieve object for CharacterObject id: " + troopId);
                    }
                    else
                    {
                        memberRoster.AddToCounts(characterObject, troopsToAdd[troopId]);
                    }
                }

                stringBuilder.AppendLine(strings[0] + " was given troops.");
            }
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Hero not found.";
    }

    /// <summary>
    /// Add prisoners to a hero's party
    /// </summary>
    [CommandLineArgumentFunction("addprisoners", "coop.debug.mobileparty")]
    public static string AddPrisonersCommand(List<string> strings)
    {
        if (ModInformation.IsClient) return "Command can only be run on the server.";

        if (strings.Count == 0) return "Hero name required";

        if (TryGetObjectManager(out var objectManager) == false) return "Unable to resolve ObjectManager.";

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var hero in Hero.AllAliveHeroes)
        {
            if (hero.Name.ToString() == strings[0])
            {
                var prisonerRoster = hero.PartyBelongedTo.PrisonRoster;
                var troopsToAdd = new Dictionary<string, int>()
                {
                    { "imperial_vigla_recruit", 5 },
                    { "imperial_recruit", 2 },
                    { "imperial_equite", 2 },
                    { "imperial_heavy_horseman", 2 }
                };

                foreach (var troopId in troopsToAdd.Keys)
                {
                    if (!objectManager.TryGetObject(troopId, out CharacterObject characterObject))
                    {
                        stringBuilder.AppendLine("Failed to retrieve object for CharacterObject id: " + troopId);
                    }
                    else
                    {
                        prisonerRoster.AddToCounts(characterObject, troopsToAdd[troopId]);
                    }
                }

                stringBuilder.AppendLine(strings[0] + " was given prisoners.");
            }
        }

        string result = stringBuilder.ToString();
        if (result.Length > 0)
        {
            return result;
        }
        return "Hero not found.";
    }
}
