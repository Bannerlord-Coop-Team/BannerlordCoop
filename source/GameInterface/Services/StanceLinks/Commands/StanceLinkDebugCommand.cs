using Common.Extensions;
using Common.Messaging;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Messages.Lifetime;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.CampaignSystem.Army;
using static TaleWorlds.CampaignSystem.StanceLink;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Armies.Commands;

/// <summary>
/// Commands for <see cref="Army"/>
/// </summary>
public class StanceLinkDebugCommand
{
    // coop.debug.stancelink.declarewar
    /// <summary>
    /// Lists all the current Army
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
            var stringBuilder = new StringBuilder();

            return $"Usage: coop.debug.stancelink.declare_war <kingdom1Id | clan1Id> <kingdom2Id | clan2Id> [isAtConstantWar=0]";
        }
        
        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            return $"Unable to get ObjectManager";
        }
        
        var faction1Id = args[0];
        var faction2Id = args[1];
        bool isAtConstantWar = (args.Count == 2) ? false : (args[2] == "1");
        bool isKingdom1 = false;
        bool isKingdom2 = false;

        if (!objectManager.TryGetObject(faction1Id, out Kingdom kingdom1))
        {
            if (!objectManager.TryGetObject(faction1Id, out Clan clan1))
            {
                return $"Unable to get Kingdom or Clan with {faction1Id}";
            }

            if (!objectManager.TryGetObject(faction2Id, out Kingdom kingdom2))
            {
                if (!objectManager.TryGetObject(faction2Id, out Clan clan2))
                {
                    return $"Unable to get Kingdom or Clan with {faction2Id}";
                }
                FactionManager.DeclareWar(clan1, clan2, isAtConstantWar);
            }
            else
            {
                FactionManager.DeclareWar(clan1, kingdom2, isAtConstantWar);
            }
        }
        else
        {
            if (!objectManager.TryGetObject(faction2Id, out Kingdom kingdom2))
            {
                if (!objectManager.TryGetObject(faction2Id, out Clan clan2))
                {
                    return $"Unable to get Kingdom or Clan with {faction2Id}";
                }
                FactionManager.DeclareWar(kingdom1, clan2, isAtConstantWar);
            }
            else
            {
                FactionManager.DeclareWar(kingdom1, kingdom2, isAtConstantWar);
            }
        }

        return $"BITE ENTRE {faction1Id} et {faction2Id}";
    }

}
