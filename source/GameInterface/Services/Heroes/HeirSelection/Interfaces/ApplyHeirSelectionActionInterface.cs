using Common.Messaging;
using GameInterface.Services.Actions.Patches;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.UI.LogEntries.Messages;
using Helpers;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace GameInterface.Services.Heroes.HeirSelection.Interfaces;

public interface IApplyHeirSelectionActionInterface : IGameAbstraction
{
    void ApplyByDeath(Hero originalHero, Hero heir);
    void ApplyByRetirement(Hero originalHero, Hero heir);
}

public class ApplyHeirSelectionActionInterface : IApplyHeirSelectionActionInterface
{
    private readonly IMessageBroker messageBroker;

    public ApplyHeirSelectionActionInterface(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
    }

    public void ApplyByDeath(Hero originalHero, Hero heir)
    {
        ApplyInternal(originalHero, heir, false);
    }

    public void ApplyByRetirement(Hero originalHero, Hero heir)
    {
        ApplyInternal(originalHero, heir, true);
    }

    private void ApplyInternal(Hero originalHero, Hero heir, bool isRetirement = false)
    {
        var originalParty = originalHero.PartyBelongedTo;

        if (heir.PartyBelongedTo != null && heir.PartyBelongedTo.IsCaravan)
        {
            Settlement settlement = SettlementHelper.FindNearestSettlementToMobileParty(
                heir.PartyBelongedTo,
                MobileParty.NavigationType.All,
                s => (s.IsTown || s.IsCastle) && !FactionManager.IsAtWarAgainstFaction(s.MapFaction, heir.MapFaction));

            settlement ??= SettlementHelper.FindNearestSettlementToMobileParty(
                heir.PartyBelongedTo,
                MobileParty.NavigationType.All,
                s => s.IsVillage || (!s.IsHideout && !s.IsFortification));

            DestroyPartyAction.Apply(null, heir.PartyBelongedTo);
            TeleportHeroAction.ApplyImmediateTeleportToSettlement(heir, settlement);
        }
        TransferCaravanOwnerships(originalHero, heir);
        ChangeClanLeaderAction.ApplyWithSelectedNewLeader(originalHero.Clan, heir);
        if (!isRetirement)
        {
            KillCharacterActionPatches.HandleKingdomLeaderDeath(originalHero);
        }
        if (isRetirement)
        {
            DisableHeroAction.Apply(originalHero);
            if (originalParty != null && heir.PartyBelongedTo != originalParty)
            {
                originalParty.MemberRoster.RemoveTroop(originalHero.CharacterObject, 1, default, 0);
            }

            // Broadcast log entry for all clients
            messageBroker.Publish(this, new LogPlayerRetired(originalHero));

            TextObject textObject = new TextObject("{=0MTzaxau}{?CHARACTER.GENDER}She{?}He{\\?} retired from adventuring, and was last seen with a group of mountain hermits living a life of quiet contemplation.", null);
            textObject.SetCharacterProperties("CHARACTER", originalHero.CharacterObject, false);
            originalHero.EncyclopediaText = textObject;
        }
        if (heir.CurrentSettlement != null && heir.PartyBelongedTo != null)
        {
            LeaveSettlementAction.ApplyForCharacterOnly(heir);
            LeaveSettlementAction.ApplyForParty(heir.PartyBelongedTo);
        }
        for (int i = originalHero.OwnedWorkshops.Count - 1; i >= 0; i--)
        {
            ChangeOwnerOfWorkshopAction.ApplyByDeath(originalHero.OwnedWorkshops[i], heir);
        }
        foreach (Alley alley in originalHero.OwnedAlleys.ToList<Alley>())
        {
            alley.SetOwner(heir);
        }
        if (originalParty != null && heir.PartyBelongedTo != originalParty)
        {
            for (int j = originalParty.MemberRoster.Count - 1; j >= 0; j--)
            {
                TroopRosterElement elementCopyAtIndex = originalParty.MemberRoster.GetElementCopyAtIndex(j);
                if (elementCopyAtIndex.Character.IsHero && !elementCopyAtIndex.Character.HeroObject.IsPlayerHero())
                {
                    MakeHeroFugitiveAction.Apply(elementCopyAtIndex.Character.HeroObject, false);
                }
            }
        }
        if (originalParty?.Army != null)
        {
            DisbandArmyAction.ApplyByUnknownReason(originalParty.Army);
        }

        messageBroker.Publish(this, new ChangePlayerCharacterAfterHeirSelection(originalHero, heir));
    }

    private void TransferCaravanOwnerships(Hero originalHero, Hero newLeader)
    {
        if (originalHero.Clan == null) return;

        foreach (Hero hero in originalHero.Clan.Heroes)
        {
            if (hero.PartyBelongedTo != null && hero.PartyBelongedTo.IsCaravan)
            {
                CaravanPartyComponent.TransferCaravanOwnership(hero.PartyBelongedTo, newLeader, hero.PartyBelongedTo.HomeSettlement);
            }
        }
    }
}
