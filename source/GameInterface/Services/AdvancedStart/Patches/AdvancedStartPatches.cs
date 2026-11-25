using GameInterface.Services.Players;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.AdvancedStart.Patches;

/// <summary>
/// Makes vanilla's world-changing advanced starts fit a shared world when the server applies one to a
/// joining player: fiefs another player owns aren't handed out, and fiefs and the mercenary start's war
/// change through actions co-op replicates.
/// </summary>
[HarmonyPatch(typeof(CampaignAdvancedStartingPlayerOptionsCampaignBehavior))]
internal class AdvancedStartPatches
{
    [HarmonyPatch(nameof(CampaignAdvancedStartingPlayerOptionsCampaignBehavior.GiveStartingFiefs))]
    [HarmonyPrefix]
    private static void GiveStartingFiefsPrefix(ref Func<Settlement, bool> condition)
    {
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager)) return;

        var vanillaCondition = condition;
        condition = settlement => !playerManager.Contains(settlement.OwnerClan) &&
                                  (vanillaCondition == null || vanillaCondition(settlement));
    }

    // Vanilla writes the owner directly, which leaves clients' clan fief lists stale and throws when the fief
    // has a governor. The action replicates, removes the governor and does the rest of what vanilla does here.
    [HarmonyPatch(nameof(CampaignAdvancedStartingPlayerOptionsCampaignBehavior.GiveSettlementToMainHero))]
    [HarmonyPrefix]
    private static bool GiveSettlementToMainHeroPrefix(Settlement settlement)
    {
        if (!settlement.IsFortification) return true;

        ChangeOwnerOfSettlementAction.ApplyByDefault(Hero.MainHero, settlement);
        return false;
    }

    // FactionManager.DeclareWar doesn't replicate, so clients would never see the mercenary start's war.
    [HarmonyPatch(nameof(CampaignAdvancedStartingPlayerOptionsCampaignBehavior.FindEnemyKingdomOrDeclareWar))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> FindEnemyKingdomOrDeclareWarTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var declareWar = AccessTools.Method(typeof(FactionManager), nameof(FactionManager.DeclareWar));
        var declareWarAction = AccessTools.Method(typeof(DeclareWarAction), nameof(DeclareWarAction.ApplyByDefault));

        foreach (var instruction in instructions)
        {
            if (instruction.Calls(declareWar))
                instruction.operand = declareWarAction;

            yield return instruction;
        }
    }
}

/// <summary>
/// Rewrites vanilla's start methods for a shared world. Vanilla rolls each start from the campaign seed, which would
/// give every player with the same start the same town, castle or workshop, so each start reads a seed mixed with
/// the hero it's applied to instead. The trader start's workshop also skips one another player owns. Public so other
/// start types' patches, such as War Sails', can run the same <see cref="Transpiler"/> over their start methods.
/// </summary>
[HarmonyPatch]
public class AdvancedStartMethodPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var type = typeof(CampaignAdvancedStartingPlayerOptionsCampaignBehavior);
        yield return AccessTools.Method(type, nameof(CampaignAdvancedStartingPlayerOptionsCampaignBehavior.StartGameAsRuler));
        yield return AccessTools.Method(type, nameof(CampaignAdvancedStartingPlayerOptionsCampaignBehavior.StartGameAsVassal));
        yield return AccessTools.Method(type, nameof(CampaignAdvancedStartingPlayerOptionsCampaignBehavior.StartGameAsMercenary));
        yield return AccessTools.Method(type, nameof(CampaignAdvancedStartingPlayerOptionsCampaignBehavior.StartGameAsTrader));
        yield return AccessTools.Method(type, nameof(CampaignAdvancedStartingPlayerOptionsCampaignBehavior.StartGameAsOutlaw));
        yield return AccessTools.Method(type, nameof(CampaignAdvancedStartingPlayerOptionsCampaignBehavior.StartGameAsBeggar));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var getSeed = AccessTools.PropertyGetter(typeof(CampaignOptions), nameof(CampaignOptions.Seed));
        var getPlayerSeed = AccessTools.Method(typeof(AdvancedStartMethodPatches), nameof(GetPlayerSeed));
        var applyByFree = AccessTools.Method(typeof(ChangeOwnerOfWorkshopAction), nameof(ChangeOwnerOfWorkshopAction.ApplyByFree));
        var giveTraderWorkshop = AccessTools.Method(typeof(AdvancedStartMethodPatches), nameof(GiveTraderWorkshop));

        foreach (var instruction in instructions)
        {
            if (instruction.Calls(getSeed))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = getPlayerSeed;
            }
            else if (instruction.Calls(applyByFree))
            {
                instruction.operand = giveTraderWorkshop;
            }

            yield return instruction;
        }
    }

    // Hero.MainHero is the joining hero while the server applies their start.
    private static uint GetPlayerSeed(CampaignOptions options) =>
        options.Seed ^ (uint)Hero.MainHero.StringId.GetHashCode();

    // Vanilla hands the trader a random workshop whoever owns it, so a second trader could take the first one's.
    private static void GiveTraderWorkshop(Workshop workshop, Hero newOwner)
    {
        if (ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) && playerManager.Contains(workshop.Owner))
        {
            workshop = workshop.Settlement.Town.Workshops.FirstOrDefault(candidate =>
                !candidate.WorkshopType.IsHidden && !playerManager.Contains(candidate.Owner));
            if (workshop == null) return;
        }

        ChangeOwnerOfWorkshopAction.ApplyByFree(workshop, newOwner);
    }
}
