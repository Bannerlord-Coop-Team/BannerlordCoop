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

namespace GameInterface.Services.PartyComponents.Patches.Lifetime;

/// <summary>
/// Harmony patches for the lifetime of a <see cref="PatrolPartyComponent"/> object.
///
/// The Prefix fires before the constructor body, so <c>_homeSettlement</c> is not yet
/// assigned to the instance. However, Harmony delivers the constructor parameters by
/// name, so <c>homeSettlement</c> and <c>isNaval</c> are available directly. They are
/// bundled into <see cref="PartyComponentCreated"/> via <c>SettlementId</c> so that
/// <see cref="Handlers.PartyComponentHandler"/> can reconstruct the full component state
/// on the client without any dependency on DynamicSync message ordering.
/// </summary>
[HarmonyPatch]
internal class PatrolPartyComponentLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PatrolPartyComponentLifetimePatches>();

    private static IEnumerable<MethodBase> TargetMethods() =>
        AccessTools.GetDeclaredConstructors(typeof(PatrolPartyComponent));

    private static bool Prefix(PatrolPartyComponent __instance, Settlement homeSettlement, bool isNaval)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created managed {name}", typeof(PatrolPartyComponent));
            return true;
        }

        var message = new PartyComponentCreated(__instance,
            SettlementId: homeSettlement?.StringId,
            IsNaval: isNaval);
        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}
