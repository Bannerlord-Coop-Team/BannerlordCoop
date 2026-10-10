using Common;
using Common.Logging;
using HarmonyLib;
using SandBox.View.Map.Managers;
using Serilog;
using System.Collections.Generic;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// Lets any joined player defender build siege defenses. Vanilla gates the pulsing tiles on the
/// single top leader matching Hero.MainHero, so only the first joiner could build and after a
/// break-out neither could. Same joined gate as the production popup: presence alone is not
/// enough, everyone needs the local "Join the defense", inside or outside. The flag stays
/// vanilla for attackers and the server.
/// </summary>
[HarmonyPatch(typeof(SettlementVisualManager), "TickSiegeMachineCircles")]
internal static class SettlementSiegeDefenseCommandPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(SettlementSiegeDefenseCommandPatch));

    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        var mainHeroGetter = AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.MainHero));
        var commandCheck = AccessTools.Method(
            typeof(SettlementSiegeDefenseCommandPatch),
            nameof(IsCommandCapable));

        int replacements = 0;
        for (int i = 0; i + 1 < codes.Count; i++)
        {
            if (!codes[i].Calls(mainHeroGetter)) continue;
            if (codes[i + 1].opcode != OpCodes.Ceq) continue;

            var call = new CodeInstruction(OpCodes.Call, commandCheck);
            call.labels.AddRange(codes[i].labels);
            call.labels.AddRange(codes[i + 1].labels);
            call.blocks.AddRange(codes[i + 1].blocks);

            codes[i] = call;
            codes.RemoveAt(i + 1);
            replacements++;
        }

        if (replacements == 0)
            Logger.Warning("{Patch} made no replacements; the target method may have changed", nameof(Transpiler));
        else
            Logger.Debug("{Patch} replaced {Count} Hero.MainHero comparison(s)", nameof(Transpiler), replacements);

        return codes;
    }

    private static bool IsCommandCapable(Hero leader)
    {
        bool vanilla = false;
        try { vanilla = leader != null && leader == Hero.MainHero; }
        catch { vanilla = false; }

        try
        {
            if (ModInformation.IsServer) return vanilla;

            BattleSideEnum side;
            try { side = PlayerSiege.PlayerSide; }
            catch { return vanilla; }
            if (side != BattleSideEnum.Defender) return vanilla;

            SiegeEvent siege;
            try { siege = PlayerSiege.PlayerSiegeEvent; }
            catch { return vanilla; }
            if (siege == null) return vanilla;

            MobileParty mainParty;
            try { mainParty = MobileParty.MainParty; }
            catch { return vanilla; }

            // Consistent with the popup gate: only joined defenders highlight, inside or outside.
            // Presence alone (including vanilla's PlayerSiege resolution) is not joining.
            return MapSiegeCommandAuthorityPatch.IsLocalDefenderJoined(mainParty, siege);
        }
        catch
        {
            return vanilla;
        }
    }
}
