using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans.Patches
{
    [HarmonyPatch(typeof(ApplyHeirSelectionAction), "ApplyInternal")]
    public class ClanHeirSelectionPatch
    {
        public static bool Prefix(Hero heir, bool isRetirement = false)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            string playerHeroId = Hero.MainHero.StringId;

            MessageBroker.Instance.Publish(heir, new NewHeirAdded(heir.StringId, playerHeroId, isRetirement));

            return false;
        }

        public static void RunFixedHeirSelectionPatchHero(Hero heir, Hero playerHero, bool isRetirement = false)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                if (heir.PartyBelongedTo != null && heir.PartyBelongedTo.IsCaravan)
                {
                    Settlement settlement = SettlementHelper.FindNearestSettlement((Settlement s) => (s.IsTown || s.IsCastle) && !FactionManager.IsAtWarAgainstFaction(s.MapFaction, heir.MapFaction), null);
                    if (settlement == null)
                    {
                        settlement = SettlementHelper.FindNearestSettlement((Settlement s) => s.IsVillage || (!s.IsHideout && !s.IsFortification), null);
                    }
                    DestroyPartyAction.Apply(null, heir.PartyBelongedTo);
                    TeleportHeroAction.ApplyImmediateTeleportToSettlement(heir, settlement);
                }
                TransferCaravanOwnerships(heir, playerHero.Clan);
                ChangeClanLeaderAction.ApplyWithSelectedNewLeader(playerHero.Clan, heir);
                if (isRetirement)
                {
                    DisableHeroAction.Apply(playerHero);
                    if (heir.PartyBelongedTo != playerHero.PartyBelongedTo)
                    {
                        playerHero.PartyBelongedTo.MemberRoster.RemoveTroop(CharacterObject.PlayerCharacter, 1, default(UniqueTroopDescriptor), 0);
                    }
                    LogEntry.AddLogEntry(new PlayerRetiredLogEntry(playerHero));
                    TextObject textObject = new TextObject("{=0MTzaxau}{?CHARACTER.GENDER}She{?}He{\\?} retired from adventuring, and was last seen with a group of mountain hermits living a life of quiet contemplation.", null);
                    textObject.SetCharacterProperties("CHARACTER", playerHero.CharacterObject, false);
                    playerHero.EncyclopediaText = textObject;
                }
                else
                {
                    KillCharacterAction.ApplyByDeathMarkForced(playerHero, true);
                }
                if (heir.CurrentSettlement != null && heir.PartyBelongedTo != null)
                {
                    LeaveSettlementAction.ApplyForCharacterOnly(heir);
                    LeaveSettlementAction.ApplyForParty(heir.PartyBelongedTo);
                }
                for (int i = playerHero.OwnedWorkshops.Count - 1; i >= 0; i--)
                {
                    ChangeOwnerOfWorkshopAction.ApplyByDeath(playerHero.OwnedWorkshops[i], heir);
                }
                if (heir.PartyBelongedTo != playerHero.PartyBelongedTo)
                {
                    for (int j = playerHero.PartyBelongedTo.MemberRoster.Count - 1; j >= 0; j--)
                    {
                        TroopRosterElement elementCopyAtIndex = playerHero.PartyBelongedTo.MemberRoster.GetElementCopyAtIndex(j);
                        if (elementCopyAtIndex.Character.IsHero && elementCopyAtIndex.Character.HeroObject != playerHero)
                        {
                            MakeHeroFugitiveAction.Apply(elementCopyAtIndex.Character.HeroObject);
                        }
                    }
                }
                if (playerHero.PartyBelongedTo.Army != null)
                {
                    DisbandArmyAction.ApplyByUnknownReason(playerHero.PartyBelongedTo.Army);
                }
                ChangePlayerCharacterAction.Apply(heir);
                Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;

            }, true);
        }
        private static void TransferCaravanOwnerships(Hero newLeader, Clan playerClan)
        {
            foreach (Hero hero in playerClan.Heroes)
            {
                if (hero.PartyBelongedTo != null && hero.PartyBelongedTo.IsCaravan)
                {
                    CaravanPartyComponent.TransferCaravanOwnership(hero.PartyBelongedTo, newLeader, hero.PartyBelongedTo.HomeSettlement);
                }
            }
        }
    }
}
