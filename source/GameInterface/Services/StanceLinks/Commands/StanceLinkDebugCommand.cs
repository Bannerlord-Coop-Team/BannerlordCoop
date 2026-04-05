using Common;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Armies.Commands;

/// <summary>
/// Commands for <see cref="Army"/>
/// </summary>
public class StanceLinkDebugCommand
{
    // coop.debug.stancelink.declare_war
    /// <summary>
    /// Declares war between 2 factions
    /// </summary>
    [CommandLineArgumentFunction("declare_war", "coop.debug.stancelink")]
    public static string DeclareWar(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return $"Command is only available to run on the server";
        }
        
        if (args.Count != 3 && args.Count != 2)
        {
            return $"Usage: coop.debug.stancelink.declare_war <kingdom1Id | clan1Id> <kingdom2Id | clan2Id> [isAtConstantWar=0]";
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get ObjectManager";
        }

        var faction1Id = args[0];
        var faction2Id = args[1];
        bool isAtConstantWar = (args.Count == 2) ? false : (args[2] == "1");

        IFaction faction1 = getFactionFromID(faction1Id, objectManager);
        if (faction1 == null)
        {
            return $"Unable to get Kingdom or Clan with {faction1Id}";
        }
        IFaction faction2 = getFactionFromID(faction2Id, objectManager);
        if (faction2 == null)
        {
            return $"Unable to get Kingdom or Clan with {faction2Id}";
        }

        FactionManager.DeclareWar(faction1, faction2);
        return $"War declared between {faction1Id} and {faction2Id}";
    }


    // coop.debug.stancelink.set_neutral
    /// <summary>
    /// Sets two factions relations to neutral
    /// </summary>
    [CommandLineArgumentFunction("set_neutral", "coop.debug.stancelink")]
    public static string SetNeutral(List<string> args)
    {
        if (ModInformation.IsClient)
        {
            return $"Command is only available to run on the server";
        }

        if (args.Count != 2)
        {
            return $"Usage: coop.debug.stancelink.set_neutral <kingdom1Id | clan1Id> <kingdom2Id | clan2Id>";
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get ObjectManager";
        }

        var faction1Id = args[0];
        var faction2Id = args[1];

        IFaction faction1 = getFactionFromID(faction1Id, objectManager);
        if (faction1 == null)
        {
            return $"Unable to get Kingdom or Clan with {faction1Id}";
        }
        IFaction faction2 = getFactionFromID(faction2Id, objectManager);
        if (faction2 == null)
        {
            return $"Unable to get Kingdom or Clan with {faction2Id}";
        }

        FactionManager.SetNeutral(faction1, faction2);
        return $"Relations are now neutral between {faction1Id} and {faction2Id}";
    }

    // coop.debug.stancelink.get_stance
    /// <summary>
    /// Gets stance between two factions
    /// </summary>
    [CommandLineArgumentFunction("get_stance", "coop.debug.stancelink")]
    public static string GetStance(List<string> args)
    {
        if (args.Count != 2)
        {
            return $"Usage: coop.debug.stancelink.get_stance <kingdom1Id | clan1Id> <kingdom2Id | clan2Id>";
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get ObjectManager";
        }

        var faction1Id = args[0];
        var faction2Id = args[1];

        IFaction faction1 = getFactionFromID(faction1Id, objectManager);
        if (faction1 == null)
        {
            return $"Unable to get Kingdom or Clan with {faction1Id}";
        }
        IFaction faction2 = getFactionFromID(faction2Id, objectManager);
        if (faction2 == null)
        {
            return $"Unable to get Kingdom or Clan with {faction2Id}";
        }

        if(FactionManager.IsNeutralWithFaction(faction1, faction2))
        {
            return $"Relations between {faction1Id} and {faction2Id} : Neutral";
        }
        else if (FactionManager.IsAtWarAgainstFaction(faction1, faction2))
        {
            return $"Relations between {faction1Id} and {faction2Id} : War";
        }
        return $"Relations between {faction1Id} and {faction2Id} : Undefined (check for error)";
    }


    public static IFaction getFactionFromID(string factionId, IObjectManager objectManager)
    {
        if (!objectManager.TryGetObject(factionId, out Kingdom kingdom))
        {
            if (!objectManager.TryGetObject(factionId, out Clan clan))
            {
                return null;
            }
            else
            {
                return clan;
            }
        }
        return kingdom;
    }

}
