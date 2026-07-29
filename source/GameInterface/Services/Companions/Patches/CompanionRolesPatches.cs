using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Companions.Messages;
using GameInterface.Services.Heroes.Patches;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Companions.Patches;

[HarmonyPatch(typeof(CompanionRolesCampaignBehavior))]
internal class CompanionRolesPatches
{
    private static readonly ILogger logger = LogManager.GetLogger<CompanionRolesPatches>();

    [HarmonyPatch(nameof(CompanionRolesCampaignBehavior.ClanNameSelectionIsDone))]
    [HarmonyPrefix]
    public static bool ClanNameSelectionIsDonePrefix(ref CompanionRolesCampaignBehavior __instance, string clanName)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        var message = new ClanNameSelectionDone(
            Hero.MainHero,
            Hero.OneToOneConversationHero,
            __instance.CurrentBehavior._selectedFief,
            MobileParty.MainParty,
            clanName
        );
        MessageBroker.Instance.Publish(__instance, message);

        Campaign.Current.ConversationManager.ContinueConversation();

        return false;
    }

    [HarmonyPatch(nameof(CompanionRolesCampaignBehavior.companion_fire_on_consequence))]
    [HarmonyPrefix]
    public static bool CompanionFireOnConsequencePrefix(ref CompanionRolesCampaignBehavior __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        var message = new CompanionFired(Hero.OneToOneConversationHero);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(CompanionRolesCampaignBehavior.companion_rejoin_after_emprisonment_role_on_consequence))]
    [HarmonyPrefix]
    public static bool CompanionRejoinAfterEmprisonmentRoleOnConsequencePrefix(ref CompanionRolesCampaignBehavior __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        var message = new CompanionRejoinAfterEmprisonment(
            Hero.OneToOneConversationHero,
            MobileParty.MainParty);
        MessageBroker.Instance.Publish(__instance, message);

        // AddHeroToPartyAction already blocked on client, need to update the ConversationManager on the client 
        return true;
    }

    [HarmonyPatch(nameof(CompanionRolesCampaignBehavior.companion_rescue_answer_options_join_party_consequence))]
    [HarmonyPrefix]
    public static bool CompanionRescueAnswerOptionsJoinPartyConsequencePrefix(ref CompanionRolesCampaignBehavior __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        var message = new CompanionJoinedPartyByRescue(
            Hero.OneToOneConversationHero,
            MobileParty.MainParty);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(CompanionRolesCampaignBehavior.PartyScreenClosed))]
    [HarmonyPrefix]
    public static bool PartyScreenClosedPrefix(ref CompanionRolesCampaignBehavior __instance, PartyBase leftOwnerParty, TroopRoster leftMemberRoster, TroopRoster leftPrisonRoster, PartyBase rightOwnerParty, TroopRoster rightMemberRoster, TroopRoster rightPrisonRoster, bool fromCancel)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (fromCancel) return false;

        var message = new PartyScreenClosedFromRescuing(
            leftOwnerParty,
            leftMemberRoster,
            leftPrisonRoster,
            rightOwnerParty,
            rightMemberRoster,
            rightPrisonRoster);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(CompanionRolesCampaignBehavior.end_rescue_companion))]
    [HarmonyPrefix]
    public static bool EndRescueCompanionPrefix(ref CompanionRolesCampaignBehavior __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        __instance._partyCreatedAfterRescueForCompanion = false;
        if (Hero.OneToOneConversationHero.IsPrisoner)
        {
            var message = new CompanionRescued(Hero.OneToOneConversationHero);
            MessageBroker.Instance.Publish(__instance, message);
        }

        return false;
    }

    [HarmonyPatch(nameof(CompanionRolesCampaignBehavior.companion_rejoin_after_emprisonment_role_on_condition))]
    [HarmonyPrefix]
    public static bool CompanionRejoinAfterEmprisonmentRoleOnConditionPrefix(ref CompanionRolesCampaignBehavior __instance, ref bool __result)
    {
        // Prevent players of other clans returning a companion to their party
        if (Hero.OneToOneConversationHero.Clan != Clan.PlayerClan)
        {
            __result = false;
            return false;
        }

        return true;
    }
    // Patch for server to use passed down ClientHero instead of server's MainHero
    // which leads to a different hero
    [HarmonyPatch(nameof(CompanionRolesCampaignBehavior.SpawnNewHeroesForNewCompanionClan))]
    [HarmonyPrefix]
    public static bool SpawnNewHeroesForNewCompanionClanPrefix(CompanionRolesCampaignBehavior __instance, Hero companionHero, Clan clan, Settlement settlement)
    {
        MBReadOnlyList<CharacterObject> lordTemplates = companionHero.Culture.LordTemplates;
        List<Hero> list = new List<Hero>();
        list.Add(__instance.CreateNewHeroForNewCompanionClan(lordTemplates.GetRandomElement<CharacterObject>(), settlement, new Dictionary<SkillObject, int>
            {
                {
                    DefaultSkills.Steward,
                    MBRandom.RandomInt(100, 175)
                },
                {
                    DefaultSkills.Leadership,
                    MBRandom.RandomInt(125, 175)
                },
                {
                    DefaultSkills.OneHanded,
                    MBRandom.RandomInt(125, 175)
                },
                {
                    DefaultSkills.Medicine,
                    MBRandom.RandomInt(125, 175)
                }
            }));
        list.Add(__instance.CreateNewHeroForNewCompanionClan(lordTemplates.GetRandomElement<CharacterObject>(), settlement, new Dictionary<SkillObject, int>
            {
                {
                    DefaultSkills.OneHanded,
                    MBRandom.RandomInt(100, 175)
                },
                {
                    DefaultSkills.Leadership,
                    MBRandom.RandomInt(125, 175)
                },
                {
                    DefaultSkills.Tactics,
                    MBRandom.RandomInt(125, 175)
                },
                {
                    DefaultSkills.Engineering,
                    MBRandom.RandomInt(125, 175)
                }
            }));
        list.Add(companionHero);
        foreach (Hero hero in list)
        {
            hero.Clan = clan;
            hero.ChangeState(Hero.CharacterStates.Active);
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(hero, ResolvedMainHeroContext.ResolvedMainHero, MBRandom.RandomInt(5, 10), false);
            if (hero != companionHero)
            {
                EnterSettlementAction.ApplyForCharacterOnly(hero, settlement);
            }
            foreach (Hero hero2 in list)
            {
                if (hero != hero2)
                {
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(hero, hero2, MBRandom.RandomInt(5, 10), false);
                }
            }
        }
        return false;
    }
    // Patch for server to use passed down ClientHero instead of server's MainHero
    // which leads to a different hero
    [HarmonyPatch(nameof(CompanionRolesCampaignBehavior.GetRandomBannerIdForNewClan))]
    [HarmonyPrefix]
    public static bool GetRandomBannerIdForNewClanPrefix(CompanionRolesCampaignBehavior __instance, ref int __result)
    {
        MBReadOnlyList<int> possibleClanBannerIconsIDs = ResolvedMainHeroContext.ResolvedMainHero.MapFaction.Culture.PossibleClanBannerIconsIDs;
        int num = possibleClanBannerIconsIDs.GetRandomElement<int>();
        if (__instance.CurrentBehavior._alreadyUsedIconIdsForNewClans.Contains(num))
        {
            int num2 = 0;
            do
            {
                num = possibleClanBannerIconsIDs.GetRandomElement<int>();
                num2++;
            }
            while (__instance.CurrentBehavior._alreadyUsedIconIdsForNewClans.Contains(num) && num2 < 20);
            bool flag = num2 != 20;
            if (!flag)
            {
                for (int i = 0; i < possibleClanBannerIconsIDs.Count; i++)
                {
                    if (!__instance.CurrentBehavior._alreadyUsedIconIdsForNewClans.Contains(possibleClanBannerIconsIDs[i]))
                    {
                        num = possibleClanBannerIconsIDs[i];
                        flag = true;
                        break;
                    }
                }
            }
            if (!flag)
            {
                num = possibleClanBannerIconsIDs.GetRandomElement<int>();
            }
        }
        if (!__instance.CurrentBehavior._alreadyUsedIconIdsForNewClans.Contains(num))
        {
            __instance.CurrentBehavior._alreadyUsedIconIdsForNewClans.Add(num);
        }
        __result = num;
        return false;
    }
}
