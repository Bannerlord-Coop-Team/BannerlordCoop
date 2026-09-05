using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.Core;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch]
internal static class ClanMembersVMPatches
{
    [HarmonyPatch(typeof(ClanManagementVM), MethodType.Constructor,
        typeof(Action), typeof(Action<Hero>), typeof(Action<Hero>), typeof(Action))]
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> ConstructorTranspiler(IEnumerable<CodeInstruction> instructions)
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
    private static void RefreshMembersListPostfix(ClanMembersVM __instance)
    {
        if (__instance is SharedClanMembersVM shared) shared.RegroupMembers();
    }

    [HarmonyPatch(typeof(ClanMembersVM), nameof(ClanMembersVM.SelectMember))]
    [HarmonyPrefix]
    private static bool SelectMemberPrefix(ClanMembersVM __instance, Hero hero)
    {
        return !(__instance is SharedClanMembersVM shared && shared.SelectAdditionalMember(hero));
    }

    [HarmonyPatch(typeof(ClanLordItemVM), nameof(ClanLordItemVM.UpdateProperties))]
    [HarmonyPostfix]
    internal static void UpdatePropertiesPostfix(ClanLordItemVM __instance)
    {
        var hero = __instance.GetHero();
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
}
