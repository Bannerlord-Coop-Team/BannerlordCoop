using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.PartyComponents.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.Patches;

[HarmonyPatch]
internal class PartyComponentLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroLifetimePatches>();

    private static IEnumerable<MethodBase> TargetMethods()
    {
        return AccessTools.GetDeclaredConstructors(typeof(LordPartyComponent))
            .Concat(AccessTools.GetDeclaredConstructors(typeof(BanditPartyComponent)))
            .Concat(AccessTools.GetDeclaredConstructors(typeof(CaravanPartyComponent)))
            .Concat(AccessTools.GetDeclaredConstructors(typeof(CustomPartyComponent)))
            .Concat(AccessTools.GetDeclaredConstructors(typeof(GarrisonPartyComponent)))
            .Concat(AccessTools.GetDeclaredConstructors(typeof(MilitiaPartyComponent)))
            .Concat(AccessTools.GetDeclaredConstructors(typeof(VillagerPartyComponent)));
    }

    private static bool Prefix(LordPartyComponent __instance)
    {
        // Skip if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MobileParty), Environment.StackTrace);
            return true;
        }

        var message = new PartyComponentCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}
