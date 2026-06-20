using Common;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Actions.Patches;

[HarmonyPatch(typeof(DestroyPartyAction))]
internal class DestroyPartyActionPatches
{
    [HarmonyPatch(nameof(DestroyPartyAction.ApplyInternal))]
    [HarmonyPrefix]
    public static bool ApplyInternalPrefix(PartyBase destroyerParty, MobileParty destroyedParty)
    {
        GameThread.RunSafe(() =>
        {
            if (!destroyedParty.IsPlayerParty())
            {
                if (destroyedParty.IsCaravan && destroyedParty.Party.Owner != null && destroyedParty.Party.Owner.GetPerkValue(DefaultPerks.Trade.InsurancePlans))
                {
                    GiveGoldAction.ApplyBetweenCharacters(null, destroyedParty.Party.Owner, (int)DefaultPerks.Trade.InsurancePlans.PrimaryBonus, false);
                }
                CampaignEventDispatcher.Instance.OnMobilePartyDestroyed(destroyedParty, destroyerParty);
                CampaignEventDispatcher.Instance.OnMapInteractableDestroyed(destroyedParty.Party);
                destroyedParty.RemoveParty();
            }
        });

        return false;
    }
}