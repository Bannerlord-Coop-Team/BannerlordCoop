using Common;
using Common.Messaging;
using GameInterface.Services.ItemRosters.Messages;
using HarmonyLib;
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
        if (ModInformation.IsClient) return;

        if (value != null && !ItemRosterPatch.IsRegistered(value))
        {
            MessageBroker.Instance.Publish(value, new ItemRosterCreated(value));
        }

        ItemRosterLookup.Set(value, __instance);
    }
}
