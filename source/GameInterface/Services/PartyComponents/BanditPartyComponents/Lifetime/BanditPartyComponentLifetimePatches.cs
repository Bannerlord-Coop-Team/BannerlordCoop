using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.PartyComponents.Messages;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyComponents.BanditPartyComponents.Lifetime;


/// <summary>
/// Harmony patches for the lifetime of a <see cref="BanditPartyComponent"/> object
/// </summary>
[HarmonyPatch]
internal class BanditPartyComponentLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<BanditPartyComponentLifetimePatches>();

    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(BanditPartyComponent));

    private static bool Prefix(BanditPartyComponent __instance, object[] __args)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created managed {name}", typeof(BanditPartyComponent));
            return false;
        }

        // The first argument is either a Hideout or the Settlement stored in _relatedSettlement.
        var relatedSettlement = __args[0] as Settlement;
        var message = new PartyComponentCreated(__instance, relatedSettlement?.StringId);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}
