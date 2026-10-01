using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.HeirSelection.Handlers;
using GameInterface.Services.Heroes.HeirSelection.Interfaces;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.UI.Cutscenes.Messages;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.HeirSelectionPopup;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Heroes.HeirSelection.Patches;

#if TESTER
[HarmonyPatch(typeof(HeirSelectionCampaignBehavior))]
internal class HeirSelectionCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(HeirSelectionCampaignBehavior.OnBeforeMainCharacterDied))]
    [HarmonyPrefix]
    public static bool OnBeforeMainCharacterDiedPrefix(HeirSelectionCampaignBehavior __instance, Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification = true)
    {
        if (ModInformation.IsClient || !victim.IsPlayerHero()) return false;

        if (ContainerProvider.TryResolve<IHeirSelectionCampaignBehaviorInterface>(out var succession))
            succession.PrepareSuccession(victim);

        // Rest of vanilla implementation is run from HeirSelectionHandler.Handle_PlayerHeirSelectionRequested.
        // Re-connecting clients need to process heir selection too so only broadcast cutscene message.
        MessageBroker.Instance.Publish(__instance, new InitiateCutscenePlayerCharacterDied(victim, killer, detail));

        return false;
    }

    [HarmonyPatch(nameof(HeirSelectionCampaignBehavior.OnBeforePlayerCharacterChanged))]
    [HarmonyPrefix]
    public static bool OnBeforePlayerCharacterChangedPrefix()
    {
        // Implemented by HeirSelectionCampaignBehaviorInterface on server
        return false;
    }

    [HarmonyPatch(nameof(HeirSelectionCampaignBehavior.OnPlayerCharacterChanged))]
    [HarmonyPrefix]
    public static bool OnPlayerCharacterChangedPrefix(HeirSelectionCampaignBehavior __instance, Hero oldPlayer, Hero newPlayer, MobileParty newMainParty, bool isMainPartyChanged)
    {
        var message = new PlayerCharacterChangedAfterHeirSelection(oldPlayer, newPlayer, newMainParty, isMainPartyChanged);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(HeirSelectionCampaignBehavior.OnHeirSelectionOver))]
    [HarmonyPrefix]
    public static bool OnHeirSelectionOverPrefix(HeirSelectionCampaignBehavior __instance, Hero selectedHeir)
    {
        if (ModInformation.IsServer) return false;

        var message = new HeirSelectionOver(Hero.MainHero, selectedHeir);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(typeof(HeirSelectionPopupVM), nameof(HeirSelectionPopupVM.RefreshValues))]
    [HarmonyPostfix]
    public static void RefreshValuesPostfix(HeirSelectionPopupVM __instance)
    {
        if (!ContainerProvider.TryResolve<HeirSelectionHandler>(out var handler)) return;
        bool appoint = handler.IsAppointingClanLeader;
        __instance.TitleText = GameTexts.FindText(appoint ? "str_coop_succession_appoint_title" : "str_coop_succession_heir_title").ToString();
        if (appoint) __instance.ButtonOkLabel = GameTexts.FindText("str_coop_succession_appoint_button").ToString();
    }

    [HarmonyPatch(typeof(HeirSelectionPopupVM), nameof(HeirSelectionPopupVM.ExecuteSelectHeir))]
    [HarmonyPrefix]
    public static bool ExecuteSelectHeirPrefix(HeirSelectionPopupVM __instance)
    {
        if (!ContainerProvider.TryResolve<HeirSelectionHandler>(out var handler) || !handler.IsAppointingClanLeader) return true;

        var selected = __instance.CurrentSelectedHero.Hero;
        InformationManager.ShowInquiry(new InquiryData(
            GameTexts.FindText("str_coop_succession_appoint_title").ToString(),
            GameTexts.FindText("str_coop_clan_experimental_warning") + "\n\n" +
            GameTexts.FindText("str_coop_succession_appoint_description").SetTextVariable("HERO", selected.Name),
            true, true, GameTexts.FindText("str_coop_succession_appoint_button").ToString(),
            GameTexts.FindText("str_cancel").ToString(),
            () => __instance.ExecuteFinalizeHeirSelection(selected), null));
        return false;
    }
}
#endif
