using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch]
internal static class ClanMembersVMPatches
{
    [HarmonyPatch(typeof(ClanManagementVM), MethodType.Constructor,
        typeof(Action), typeof(Action<Hero>), typeof(Action<Hero>), typeof(Action))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ConstructorTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var constructor = AccessTools.Constructor(typeof(ClanMembersVM), new[] { typeof(Action), typeof(Action<Hero>) });
        int replacements = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Newobj && Equals(instruction.operand, constructor))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(ClanMembersVMPatches), nameof(CreateMembers));
                replacements++;
            }

            yield return instruction;
        }

        if (replacements != 1)
            throw new InvalidOperationException($"Expected one ClanMembersVM construction, found {replacements}.");
    }

    private static ClanMembersVM CreateMembers(Action onRefresh, Action<Hero> showHeroOnMap)
    {
        if (ContainerProvider.TryResolve<IClanMembersVMFactory>(out var factory))
            return factory.Create(onRefresh, showHeroOnMap);

        return new ClanMembersVM(onRefresh, showHeroOnMap);
    }

    [HarmonyPatch(typeof(ClanMembersVM), nameof(ClanMembersVM.RefreshMembersList))]
    [HarmonyPostfix]
    public static void RefreshMembersListPostfix(ClanMembersVM __instance)
    {
        if (__instance is CoopClanMembersVM coop) coop.RegroupMembers();
    }

    [HarmonyPatch(typeof(ClanFiefsVM), nameof(ClanFiefsVM.GetSendMembersCandidates))]
    [HarmonyPostfix]
    public static void GetSendMembersCandidatesPostfix(ref IEnumerable<ClanCardSelectionItemInfo> __result)
    {
        __result = __result.Where(candidate => !(candidate.Identifier is Hero hero) || !hero.IsPlayerHero()).ToList();
    }

    [HarmonyPatch(typeof(ClanMembersVM), nameof(ClanMembersVM.SelectMember))]
    [HarmonyPrefix]
    public static bool SelectMemberPrefix(ClanMembersVM __instance, Hero hero)
    {
        return !(__instance is CoopClanMembersVM coop && coop.SelectAdditionalMember(hero));
    }

    [HarmonyPatch(typeof(ClanMembersVM), nameof(ClanMembersVM.CurrentSelectedMember), MethodType.Setter)]
    [HarmonyPostfix]
    public static void CurrentSelectedMemberPostfix(ClanMembersVM __instance)
    {
        if (__instance is CoopClanMembersVM coop) coop.RefreshPlayerActions();
    }

    [HarmonyPatch(typeof(ClanLordItemVM), nameof(ClanLordItemVM.UpdateProperties))]
    [HarmonyPostfix]
    public static void UpdatePropertiesPostfix(ClanLordItemVM __instance)
    {
        var hero = __instance.GetHero();
        __instance.IsFamilyMember = CoopClanPermissions.CanRenameHero(hero);
        if (hero.IsPlayerHero())
        {
            __instance.IsRecallVisible = false;
            __instance.IsRecallEnabled = false;
            var roleText = GameTexts.FindText(hero.Clan?.Leader == hero
                ? "str_coop_clan_player_leader"
                : "str_coop_clan_player");
            if (hero == Hero.MainHero)
                roleText = GameTexts.FindText("str_coop_clan_player_self").SetTextVariable("ROLE", roleText);

            __instance.RelationToMainHeroText = roleText.ToString();
        }
        else if (hero.CompanionOf?.Leader is Hero leader)
        {
            __instance.RelationToMainHeroText = CampaignUIHelper.GetHeroRelationToHeroText(hero, leader, true).ToString();
        }
    }

    [HarmonyPatch(typeof(FactionHelper), nameof(FactionHelper.IsMainClanMemberAvailableForRelocate))]
    [HarmonyPostfix]
    public static void IsMainClanMemberAvailableForRelocatePostfix(Hero hero, ref bool __result, ref TextObject explanation)
    {
        if (!__result || CoopClanPermissions.CanRecallHero(hero) ||
            (!hero.IsPlayerHero() && CoopClanPermissions.CanManageHero(hero))) return;

        __result = false;
        explanation = GameTexts.FindText("str_coop_clan_hero_recall_restricted");
    }

    [HarmonyPatch(typeof(ClanMembersVM), nameof(ClanMembersVM.OnRequestRecall))]
    [HarmonyPrefix]
    public static bool OnRequestRecallPrefix(ClanMembersVM __instance)
        => CoopClanPermissions.CanRecallHero(__instance.CurrentSelectedMember?.GetHero());

    [HarmonyPatch(typeof(ClanMembersVM), nameof(ClanMembersVM.OnConfirmRecall))]
    [HarmonyPrefix]
    public static bool OnConfirmRecallPrefix(ClanMembersVM __instance)
        => CoopClanPermissions.CanRecallHero(__instance.CurrentSelectedMember?.GetHero());

    [HarmonyPatch(typeof(ClanLordItemVM), nameof(ClanLordItemVM.ExecuteRename))]
    [HarmonyPrefix]
    public static bool ExecuteRenamePrefix(ClanLordItemVM __instance)
    {
        return CoopClanPermissions.CanRenameHero(__instance.GetHero());
    }

    [HarmonyPatch(typeof(ClanLordItemVM), nameof(ClanLordItemVM.OnNamingHeroOver))]
    [HarmonyPrefix]
    public static bool OnNamingHeroOverPrefix(ClanLordItemVM __instance)
    {
        return CoopClanPermissions.CanRenameHero(__instance.GetHero());
    }
}
