using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Hideouts;

/// <summary>
/// Native's boss cutscene only places the host's main agent and AI troops, and each client owns its hero's
/// position, so every other player moves its own hero in behind the host's leader.
/// </summary>
internal sealed class HideoutBossSceneJoin
{
    internal const float DistanceBehindLeader = 2f;
    internal const float SideSpacing = 1f;
    internal const int SideSlots = 5;
    internal const float DistanceFromBoss = 2f;

    private bool sawCutscene;
    private bool joined;

    public void Tick(HideoutPhase phase, Agent hero, Agent leader, Agent boss)
    {
        // A player who arrives after the cutscene spawns beside an ally and is left there.
        sawCutscene |= phase == HideoutPhase.Cinematic;
        // The leader stands on its mark once the cutscene is over, so its position here has settled.
        if (joined || !sawCutscene || phase is not (HideoutPhase.Conversation or HideoutPhase.Duel or HideoutPhase.BossBattle)) return;
        if (hero?.IsActive() != true || boss?.IsActive() != true) return;
        joined = true;
        hero.TeleportToPosition(leader != hero && leader?.IsActive() == true
            ? BehindLeader(leader.Position, boss.Position, hero.Origin?.UniqueSeed ?? 0)
            : BesideBoss(boss));
    }

    internal static Vec3 BehindLeader(Vec3 leader, Vec3 boss, int heroSeed)
    {
        var toBoss = (boss - leader).AsVec2;
        if (toBoss.Normalize() < 0.01f) toBoss = new Vec2(0f, 1f);
        // Each hero takes one of a row of slots so several players do not land on one spot.
        int slot = Math.Abs(heroSeed % SideSlots) - SideSlots / 2;
        return leader + (toBoss * -DistanceBehindLeader + toBoss.LeftVec() * (slot * SideSpacing)).ToVec3();
    }

    internal static Vec3 BesideBoss(Agent boss)
        => boss.Position + (boss.GetMovementDirection() * DistanceFromBoss).ToVec3();
}
