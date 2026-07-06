using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.Players;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection.HUD.KillFeed.General;

namespace GameInterface.Services.UI.Patches;

/// <summary>
/// Gives each ally player a distinct, stable kill-feed color instead of vanilla's single shared "an ally
/// scored a kill" green, so a client can tell which of their allies (or their allies' troops) got the kill.
/// The "an ally was killed" branch is left untouched, so the enemy side still reads as vanilla red
/// regardless of who did it.
/// </summary>
[HarmonyPatch(typeof(SPGeneralKillNotificationItemVM), MethodType.Constructor,
    typeof(Agent), typeof(Agent), typeof(bool), typeof(bool), typeof(bool), typeof(Action<SPGeneralKillNotificationItemVM>))]
internal class KillFeedPlayerColorPatch
{
    [HarmonyPostfix]
    private static void Postfix(SPGeneralKillNotificationItemVM __instance, Agent affectedAgent, Agent affectorAgent)
    {
        // Only the "ally scored a kill" branch (victim's team is not player-allied) is ours to recolor.
        var victimTeam = affectedAgent?.Team;
        if (victimTeam == null || !victimTeam.IsValid || victimTeam.IsPlayerAlly) return;

        // Players can PvP each other, so an enemy player killing a non-allied troop (their own side, a
        // rebellion, etc.) would otherwise also match the guard above — require the killer to actually be
        // on the client's allied side before crediting the kill to one of "our" players.
        var affectorTeam = affectorAgent?.Team;
        if (affectorTeam == null || !affectorTeam.IsValid || !affectorTeam.IsPlayerAlly) return;

        var party = GetOwningParty(affectorAgent);
        if (party == null) return;

        if (!PlayerManager.TryGetControlledObjectInfo(party, out var info)) return;

        __instance.BackgroundColor = PlayerColorAssigner.GetColor(info.ObjectControllerId);
    }

    // Agent has no Party property of its own; every concrete IAgentOriginBase that carries one names it
    // independently. PartyAgentOrigin covers vanilla troops (and, via its own IsHero special-case, a
    // player's own hero agent too); CoopAgentOrigin covers this mod's server-supplied field-battle troops,
    // whose vanilla origin type (SimpleAgentOrigin) leaves Party null for non-heroes.
    private static MobileParty GetOwningParty(Agent agent)
    {
        var party = (agent?.Origin as PartyAgentOrigin)?.Party ?? (agent?.Origin as CoopAgentOrigin)?.Party;
        return party?.MobileParty;
    }
}
