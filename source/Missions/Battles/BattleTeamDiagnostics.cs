using Common.Logging;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// TEMP diagnostic: a few seconds into the battle (after spawns settle), dump the team/agent/player state once
/// so we can see why the player's side may be absent (no Defender team? team but no agents? hero spawned but
/// not the MainAgent?). Remove once the spawn/attach is solid.
/// </summary>
public class BattleTeamDiagnostics
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleTeamDiagnostics>();

    private float timer;
    private bool logged;

    public void Tick(float dt)
    {
        if (logged) return;
        timer += dt;
        if (timer < 4f) return;
        logged = true;

        var mission = Mission.Current;
        if (mission == null) return;

        foreach (var team in mission.Teams)
        {
            // List the HERO agents on each team — pinpoints where the player's own hero, the host hero and the
            // AI-lord heroes actually land (PlayerTeam = controllable by us; ally team = not).
            var heroes = new List<string>();
            foreach (var agent in team.ActiveAgents)
                if (agent.Character != null && agent.Character.IsHero)
                    heroes.Add(agent.Character.StringId);

            Logger.Information("[BattleDiag] Team side={Side} isPlayerTeam={IsPlayer} isPlayerAlly={IsAlly} activeAgents={Count} heroes=[{Heroes}]",
                team.Side, team == mission.PlayerTeam, team == mission.PlayerAllyTeam, team.ActiveAgents.Count, string.Join(", ", heroes));
        }

        var heroChar = Hero.MainHero?.CharacterObject;
        bool heroOnField = false;
        foreach (var agent in mission.Agents)
            if (agent.Character == heroChar)
            {
                heroOnField = true;
                Logger.Information("[BattleDiag] Player hero {Char} is on team side={Side} isPlayerTeam={IsPlayer} (controller={Ctrl})",
                    heroChar.StringId, agent.Team?.Side, agent.Team == mission.PlayerTeam, agent.Controller);
                break;
            }

        var spawnLogic = mission.GetMissionBehavior<DefaultBattleMissionAgentSpawnLogic>();
        Logger.Information("[BattleDiag] AttackerTeam={Atk} DefenderTeam={Def} PlayerTeam={Player} MainAgent={Main} heroOnField={Hero} spawnEnabled(Def={SDef},Atk={SAtk}) playerSide={PSide}",
            mission.AttackerTeam != null, mission.DefenderTeam != null,
            mission.PlayerTeam?.Side.ToString() ?? "null",
            mission.MainAgent != null,
            heroOnField,
            spawnLogic?.IsSideSpawnEnabled(BattleSideEnum.Defender),
            spawnLogic?.IsSideSpawnEnabled(BattleSideEnum.Attacker),
            PartyBase.MainParty?.Side);
    }
}
