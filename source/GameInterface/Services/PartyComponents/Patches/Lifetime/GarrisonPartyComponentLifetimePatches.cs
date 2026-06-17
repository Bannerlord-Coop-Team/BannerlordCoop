using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.PartyComponents.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyComponents.Patches.Lifetime;


/// <summary>
/// Harmony patches for the lifetime of a <see cref="GarrisonPartyComponent"/> object.
///
/// The constructor parameter <c>settlement</c> is bundled into
/// <see cref="PartyComponentCreated"/> so that <see cref="Handlers.PartyComponentHandler"/>
/// can restore the <see cref="GarrisonPartyComponent.Settlement"/> link on the client without
/// depending on DynamicSync field-update ordering. Without it the client receives a garrison
/// with a null Settlement, and <see cref="GarrisonPartyComponent.PartyOwner"/>
/// (Settlement.OwnerClan.Leader) throws when the nameplate VM reads it.
/// </summary>
[HarmonyPatch]
internal class GarrisonPartyComponentLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<GarrisonPartyComponentLifetimePatches>();

    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(GarrisonPartyComponent));

    private static bool Prefix(GarrisonPartyComponent __instance, Settlement settlement)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created managed {name}", typeof(GarrisonPartyComponent));
            return false;
        }

        var message = new PartyComponentCreated(__instance, settlement?.StringId);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}