using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(ClanPartiesVM))]
internal class ClanPartiesVMPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClanPartiesVMPatches>();

    [HarmonyPatch(nameof(ClanPartiesVM.CreateNewClanParty))]
    [HarmonyPrefix]
    public static bool CreateNewClanPartyPrefix(ClanPartiesVM __instance, Hero newLeader, int partyGoldLowerThreshold)
    {
        // Reject forming a new party with a player hero
        if (newLeader.IsPlayerHero())
        {
            Logger.Error($"Rejecting new clan mobile party because newLeader is a player hero ({newLeader.StringId}).");
            return false;
        }

        if (newLeader.PartyBelongedTo == MobileParty.MainParty)
        {
            __instance._openPartyAsManage(newLeader);
            __instance.RefreshPartiesList();
            return false;
        }

        // Create and manage the new mobile party on the server
        var message = new NewClanPartyCreated(Hero.MainHero, newLeader, __instance._faction, partyGoldLowerThreshold);
        MessageBroker.Instance.Publish(__instance, message);

        __instance._onRefresh();

        return false;
    }

    /// <summary>
    /// The party the change-leader popup was opened for. Save to use in OnPartyLeaderChangedPrefix.
    /// Any incoming refresh messages can change ClanPartiesVM.CurrentSelectedParty to the player's party.
    /// </summary>
    private static MobileParty popupParty;

    [HarmonyPatch(nameof(ClanPartiesVM.OnShowChangeLeaderPopup))]
    [HarmonyPrefix]
    public static void OnShowChangeLeaderPopupPrefix(ClanPartiesVM __instance)
    {
        popupParty = __instance.CurrentSelectedParty?.Party?.MobileParty;
    }

    [HarmonyPatch(nameof(ClanPartiesVM.OnPartyLeaderChanged))]
    [HarmonyPrefix]
    public static bool OnPartyLeaderChangedPrefix(ClanPartiesVM __instance, Hero newLeader)
    {
        // Use popupParty instead of the CurrentSelectedParty that can change from any incoming refresh messages
        var selectedParty = popupParty ?? __instance.CurrentSelectedParty?.Party?.MobileParty;
        popupParty = null;

        var oldLeader = selectedParty?.Party?.LeaderHero;
        if (oldLeader.IsPlayerHero())
        {
            Logger.Error($"Rejecting change of leader in clan mobile party because oldLeader is a player hero ({newLeader.StringId}).");
            return false;
        }

        // Change clan party leader on the server
        var message = new ClanPartyLeaderChanged(Hero.MainHero, newLeader, oldLeader, selectedParty, MobileParty.MainParty);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(ClanPartiesVM.OnDisbandCurrentParty))]
    [HarmonyPrefix]
    public static bool OnDisbandCurrentPartyPrefix()
    {
        // Block and implement as part of OnPartyLeaderChanged to use correct party
        // instead of currently selected (which can switch back to the player's party)
        return false;
    }
}
