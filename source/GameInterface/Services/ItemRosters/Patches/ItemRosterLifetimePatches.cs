using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.ItemRosters.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.ItemRosters.Patches;

[HarmonyPatch(typeof(PartyBase))]
internal class PartyBasePatch
{
    [HarmonyPatch(nameof(PartyBase.ItemRoster), MethodType.Setter)]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void ItemRosterSetterPrefix(PartyBase __instance, ItemRoster value)
    {
        if (!ModInformation.IsClient &&
            !CallOriginalPolicy.IsOriginalAllowed() &&
            value != null &&
            !ItemRosterPatch.IsRegistered(value))
        {
            // ItemRoster's tiny constructor can be inlined by the runtime, bypassing its lifetime patch.
            // Assignment to a PartyBase is the reliable boundary that proves this is persistent state.
            // Register synchronously before the generated ItemRoster reference-sync prefix resolves its id.
            MessageBroker.Instance.Publish(null, new ItemRosterCreated(value));
        }

        if (ModInformation.IsClient) return;

        ItemRosterLookup.Set(value, __instance);
    }
}

[HarmonyPatch]
internal class ItemRosterLifetimePatches
{
    private static ILogger Logger = LogManager.GetLogger<ItemRosterLifetimePatches>();

    [HarmonyPatch(typeof(ItemRoster), MethodType.Constructor)]
    [HarmonyPrefix]
    private static bool Prefix(ItemRoster __instance)
    {
        // Run original if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created managed {name}", typeof(ItemRoster));
            return true;
        }

        var message = new ItemRosterCreated(__instance);

        MessageBroker.Instance.Publish(null, message);

        return true;
    }
}
