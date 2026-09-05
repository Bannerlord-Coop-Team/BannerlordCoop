using HarmonyLib;
using SandBox.View.Map;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch]
internal static class ClanManagementVMPatches
{
    [HarmonyPatch(typeof(ClanManagementVM), MethodType.Constructor,
        typeof(Action), typeof(Action<Hero>), typeof(Action<Hero>), typeof(Action))]
    [HarmonyPostfix]
    public static void ConstructorPostfix(ClanManagementVM __instance)
    {
        __instance.RenownHint = new BasicTooltipViewModel(() => CampaignUIHelper.GetClanRenownTooltip(__instance._clan));
        RefreshValuesPostfix(__instance);
    }

    [HarmonyPatch(typeof(ClanManagementVM), MethodType.Constructor,
        typeof(Action), typeof(Action<Hero>), typeof(Action<Hero>), typeof(Action))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ConstructorTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return ReplacePlayerClan(instructions, 5);
    }

    [HarmonyPatch(typeof(ClanManagementVM), nameof(ClanManagementVM.RefreshValues))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> RefreshValuesTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return ReplacePlayerClan(instructions, 1);
    }

    [HarmonyPatch(typeof(ClanManagementVM), nameof(ClanManagementVM.RefreshValues))]
    [HarmonyPostfix]
    public static void RefreshValuesPostfix(ClanManagementVM __instance)
    {
        bool canManage = SharedClanPermissions.CanManageClan(__instance._clan);
        var disabledReason = GameTexts.FindText("str_coop_clan_identity_leader_only");
        __instance.CanChooseBanner = canManage;
        __instance.ClanBannerHint = new HintViewModel(canManage
            ? new TextObject("{=Nkue5MX8}Click to edit your clan's banner") : disabledReason);
        __instance.PlayerCanChangeClanName = canManage &&
            __instance.GetPlayerCanChangeClanNameWithReason(out disabledReason);
        __instance.ChangeClanNameHint = new HintViewModel(disabledReason);
    }

    [HarmonyPatch(typeof(ClanManagementVM), nameof(ClanManagementVM.ExecuteOpenBannerEditor))]
    [HarmonyPrefix]
    public static bool ExecuteOpenBannerEditorPrefix(ClanManagementVM __instance)
    {
        return SharedClanPermissions.CanManageClan(__instance._clan);
    }

    [HarmonyPatch(typeof(ClanManagementVM), nameof(ClanManagementVM.ExecuteChangeClanName))]
    [HarmonyPrefix]
    public static bool ExecuteChangeClanNamePrefix(ClanManagementVM __instance)
    {
        return SharedClanPermissions.CanManageClan(__instance._clan);
    }

    [HarmonyPatch(typeof(ClanManagementVM), nameof(ClanManagementVM.OnChangeClanNameDone))]
    [HarmonyPrefix]
    public static bool OnChangeClanNameDonePrefix(ClanManagementVM __instance)
    {
        return SharedClanPermissions.CanManageClan(__instance._clan);
    }

    [HarmonyPatch(typeof(MapScreen), nameof(MapScreen.OpenBannerEditorScreen))]
    [HarmonyPrefix]
    public static bool OpenBannerEditorScreenPrefix()
    {
        return SharedClanPermissions.CanManageClan(Hero.MainHero.Clan);
    }

    private static IEnumerable<CodeInstruction> ReplacePlayerClan(IEnumerable<CodeInstruction> instructions, int expectedReplacements)
    {
        var playerClanGetter = AccessTools.PropertyGetter(typeof(Clan), nameof(Clan.PlayerClan));
        var clanField = AccessTools.Field(typeof(ClanManagementVM), nameof(ClanManagementVM._clan));
        int replacements = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(playerClanGetter))
            {
                instruction.opcode = OpCodes.Ldarg_0;
                instruction.operand = null;
                yield return instruction;
                yield return new CodeInstruction(OpCodes.Ldfld, clanField);
                replacements++;
            }
            else
            {
                yield return instruction;
            }
        }

        if (replacements != expectedReplacements)
            throw new InvalidOperationException($"Expected {expectedReplacements} player clan lookups in clan management, found {replacements}.");
    }
}
