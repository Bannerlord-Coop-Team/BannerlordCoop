using Common.Messaging;
using GameInterface.Services.Actions.Patches;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.UI.LogEntries.Messages;
using GameInterface.Services.Workshops.Interfaces;
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
    void ApplyByDeath(Hero originalHero, Hero heir, MobileParty originalParty);
    void ApplyByRetirement(Hero originalHero, Hero heir);
    void ApplyByRetirementWithoutHeir(Hero originalHero);
    void AppointClanLeader(Hero originalHero, Hero successor);
}

public class ApplyHeirSelectionActionInterface : IApplyHeirSelectionActionInterface
{
    private readonly IMessageBroker messageBroker;
    private readonly ISessionWorkshopPlayerDataInterface workshopData;

    public ApplyHeirSelectionActionInterface(IMessageBroker messageBroker, ISessionWorkshopPlayerDataInterface workshopData)
    {
        this.messageBroker = messageBroker;
        this.workshopData = workshopData;
    }

    public void ApplyByDeath(Hero originalHero, Hero heir, MobileParty originalParty)
    {
        ApplyInternal(originalHero, heir, originalParty);
    }

    public void ApplyByRetirement(Hero originalHero, Hero heir)
    {
        ApplyInternal(originalHero, heir, originalHero.PartyBelongedTo, true);
    }

    public void ApplyByRetirementWithoutHeir(Hero originalHero)
    {
        Clan clan = originalHero.Clan;
        if (clan?.Leader == originalHero)
        {
            Hero successor = clan.Heroes
                .Where(candidate => CanSucceedRetiringLeader(candidate, originalHero))
                .OrderBy(candidate => candidate.IsPlayerHero())
                .FirstOrDefault();

            if (successor != null)
            {
                ChangeClanLeaderAction.ApplyWithSelectedNewLeader(clan, successor);
            }
            else
            {
                // The vanilla leader change action leaves the retired hero as leader when there is no successor.
                clan.SetLeader(null);
            }
        }

        DisableHeroAction.Apply(originalHero);
        RecordRetirement(originalHero);
    }

    public void AppointClanLeader(Hero originalHero, Hero successor)
    {
        ChangeClanLeaderAction.ApplyWithSelectedNewLeader(originalHero.Clan, successor);
        TransferAssets(originalHero, successor);
        workshopData.TransferWarehouseData(originalHero, successor);
        KillCharacterActionPatches.HandleKingdomLeaderDeath(originalHero);
    }

    private void ApplyInternal(Hero originalHero, Hero heir, MobileParty originalParty, bool isRetirement = false)
    {
        var heirParty = heir.PartyBelongedTo;
        if (heirParty != null && !heirParty.IsCaravan)
        {
            if (heirParty == originalParty && heirParty.LordPartyComponent != null)
            {
                heirParty.LordPartyComponent.ChangePartyOwner(heir);
                heirParty.ChangePartyLeader(heir);
            }
            else if (heirParty.LeaderHero != heir)
            {
                // Inheriting a relative must not take over another player's party.
                heirParty.RemoveAllPartyRolesOfHero(heir);
                heirParty.MemberRoster.RemoveTroop(heir.CharacterObject);
            }
        }

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
        if (originalHero.Clan.Leader == originalHero)
        {
            ChangeClanLeaderAction.ApplyWithSelectedNewLeader(originalHero.Clan, heir);
            if (!isRetirement) KillCharacterActionPatches.HandleKingdomLeaderDeath(originalHero);
        }
        else
        {
            if (heir.GovernorOf != null) ChangeGovernorAction.RemoveGovernorOf(heir);
            GiveGoldAction.ApplyBetweenCharacters(originalHero, heir, originalHero.Gold, disableNotification: true);
        }
        if (isRetirement)
        {
            DisableHeroAction.Apply(originalHero);
            if (originalParty != null && heir.PartyBelongedTo != originalParty)
            {
                originalParty.MemberRoster.RemoveTroop(originalHero.CharacterObject, 1, default, 0);
            }

            RecordRetirement(originalHero);
        }
        if (heir.CurrentSettlement != null && heir.PartyBelongedTo != null)
        {
            LeaveSettlementAction.ApplyForCharacterOnly(heir);
            LeaveSettlementAction.ApplyForParty(heir.PartyBelongedTo);
        }
        TransferAssets(originalHero, heir);
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

    private void TransferAssets(Hero originalHero, Hero heir)
    {
        foreach (var caravan in originalHero.OwnedCaravans.ToArray())
            CaravanPartyComponent.TransferCaravanOwnership(caravan.MobileParty, heir, caravan.HomeSettlement);

        for (int i = originalHero.OwnedWorkshops.Count - 1; i >= 0; i--)
            ChangeOwnerOfWorkshopAction.ApplyByDeath(originalHero.OwnedWorkshops[i], heir);

        foreach (Alley alley in originalHero.OwnedAlleys.ToArray())
            alley.SetOwner(heir);
    }

    private void RecordRetirement(Hero originalHero)
    {
        messageBroker.Publish(this, new LogPlayerRetired(originalHero));

        TextObject textObject = new TextObject("{=0MTzaxau}{?CHARACTER.GENDER}She{?}He{\\?} retired from adventuring, and was last seen with a group of mountain hermits living a life of quiet contemplation.", null);
        textObject.SetCharacterProperties("CHARACTER", originalHero.CharacterObject, false);
        originalHero.EncyclopediaText = textObject;
    }

    private static bool CanSucceedRetiringLeader(Hero candidate, Hero retiringHero)
    {
        if (candidate == retiringHero || !candidate.IsAlive || candidate.IsDisabled || candidate.IsNotSpawned ||
            candidate.IsWanderer || candidate.IsNotable || candidate.IsChild ||
            candidate.Age < Campaign.Current.Models.AgeModel.HeroComesOfAge ||
            candidate.DeathMark != KillCharacterAction.KillCharacterActionDetail.None ||
            candidate.IsPrisoner || candidate.IsFugitive || candidate.IsReleased || candidate.IsTraveling)
        {
            return false;
        }

        MobileParty party = candidate.PartyBelongedTo;
        if (retiringHero.PartyBelongedTo != null && party == retiringHero.PartyBelongedTo) return false;
        if (party?.IsPlayerParty() == true)
            return candidate.IsPlayerHero() && party.LeaderHero == candidate;

        return !candidate.IsPlayerHero() || party != null && party.LeaderHero == candidate;
    }
}
