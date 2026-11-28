using Common.Commands;
using Newtonsoft.Json;
using SandBox;
using SandBox.Missions;
using SandBox.Missions.AgentBehaviors;
using SandBox.Objects.Usables;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Hideouts;

/// <summary>Live-test commands for the co-op hideout missions on this client.</summary>
internal static class HideoutDebugCommands
{
    private static CoopCommandResult Failed(string output, string errorCode) => new CoopCommandResult(false, output, errorCode);

    private static bool TryGetAmbush(out Mission mission, out CoopHideoutMissionLogic logic, out CoopHideoutAmbushController native)
    {
        mission = Mission.Current;
        logic = mission?.GetMissionBehavior<CoopHideoutMissionLogic>();
        native = logic?.Native as CoopHideoutAmbushController;
        return native != null;
    }

    /// <summary>Has this client's hero light the nearest usable signal fire the way the use key does.</summary>
    public sealed class LightSignalFireCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hideout";
        public string Name => "light_signal_fire";
        public string Description => "Has this client's hero light the nearest usable signal fire in a stealth hideout through native's use path.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetAmbush(out var mission, out _, out var native))
                return Failed("Not in a co-op stealth hideout mission.", "mission_unavailable");
            // Outside stealth the fire's OnUse would still spend it and pause the AI, with no call to answer it.
            if (native.Phase != HideoutPhase.Stealth)
                return Failed($"The hideout is in its {native.Phase} phase; the fire can only be lit while sneaking.", "phase_unavailable");
            var hero = mission.MainAgent;
            if (hero?.IsActive() != true)
                return Failed("This client's hero isn't on the field.", "hero_unavailable");
            var fires = mission.ActiveMissionObjects.FindAllWithType<StealthAreaUsePoint>()
                .OrderBy(point => point.GameEntity.GlobalPosition.DistanceSquared(hero.Position))
                .ToArray();
            if (fires.Length == 0)
                return Failed("This hideout has no signal fire.", "fire_unavailable");
            // The gates MissionMainAgentInteractionComponent checks before the use key calls HandleStartUsingAction.
            if (!mission.IsMainAgentObjectInteractionEnabled || hero.IsUsingGameObject || !hero.IsAbleToUseMachine())
                return Failed($"This client's hero can't use objects now (interaction={mission.IsMainAgentObjectInteractionEnabled}, " +
                    $"using={hero.IsUsingGameObject}, able={hero.IsAbleToUseMachine()}).", "hero_busy");
            var fire = fires.FirstOrDefault(point => hero.ObjectHasVacantPosition(point) && hero.CanUseObject(point));
            if (fire == null)
            {
                var nearest = fires[0];
                return Failed($"No signal fire can be lit now (nearest {nearest.Id.Id}: used={nearest._isAlreadyUsed}, " +
                    $"enabled={nearest._isEnabled}, vacant={hero.ObjectHasVacantPosition(nearest)}, " +
                    $"in combat={nearest.IsInCombat()}).", "fire_unusable");
            }
            hero.HandleStartUsingAction(fire, -1);
            return new CoopCommandResult(true, $"Lit signal fire {fire.Id.Id}.");
        }
    }

    /// <summary>Has the nearest sentry spot this client's hero, the way a sentry that sees the player alarms itself.</summary>
    public sealed class AlarmNearestSentryCoopCommand : ICoopCommand
    {
        // AlarmedBehaviorGroup's AlarmFactor cap, the value its sighting branch sets.
        private const float FullAlarmFactor = 2f;

        public string Prefix => "coop.debug.hideout";
        public string Name => "alarm_nearest_sentry";
        public string Description => "On the battle host, has the nearest sentry spot this client's hero in a stealth hideout.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetAmbush(out var mission, out var logic, out var native))
                return Failed("Not in a co-op stealth hideout mission.", "mission_unavailable");
            // Only the battle host runs the sentries' AI; elsewhere they are puppets without an alarm behavior.
            if (!logic.IsAuthority)
                return Failed("Run this on the battle host.", "battle_host_only");
            if (native.Phase != HideoutPhase.Stealth)
                return Failed($"The hideout is in its {native.Phase} phase; sentries only spot someone while sneaking.", "phase_unavailable");
            var hero = mission.MainAgent;
            if (hero?.IsActive() != true)
                return Failed("This client's hero isn't on the field.", "hero_unavailable");
            static AlarmedBehaviorGroup GetAlarm(Agent agent)
                => agent.GetComponent<CampaignAgentComponent>()?.AgentNavigator?.GetBehaviorGroup<AlarmedBehaviorGroup>();

            var sentry = mission.Agents
                .Where(agent => agent.IsActive() && agent.IsHuman && agent.Team?.Side == BattleSideEnum.Defender && GetAlarm(agent) != null)
                .OrderBy(agent => agent.Position.DistanceSquared(hero.Position))
                .FirstOrDefault();
            if (sentry == null)
                return Failed("No standing sentry can spot anyone.", "sentry_unavailable");
            // Close to AlarmedBehaviorGroup's sighting branch: fill the alarm toward the hero, then escalate
            // to Alarmed, which the engine reports through OnAgentAlarmedStateChanged.
            GetAlarm(sentry).AddAlarmFactor(FullAlarmFactor, hero.GetWorldPosition());
            sentry.SetAlarmState(Agent.AIStateFlag.Alarmed);
            return new CoopCommandResult(true, $"Sentry {sentry.Index} spotted the hero.");
        }
    }

    /// <summary>Prints the stealth phase, the signal fires, the troops on the field and the sentries still standing.</summary>
    public sealed class StealthStatusCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hideout";
        public string Name => "stealth_status";
        public string Description => "Prints this client's stealth hideout phase, signal fires, troops and active sentries.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetAmbush(out var mission, out var logic, out var native))
                return Failed("Not in a co-op stealth hideout mission.", "mission_unavailable");
            var areas = mission.GetMissionBehavior<CoopHideoutStealthAreaLogic>();
            var humans = mission.Agents.Where(agent => agent.IsActive() && agent.IsHuman).ToArray();
            var attackers = humans.Where(agent => agent.Team?.Side == BattleSideEnum.Attacker).ToArray();
            var troops = attackers.Where(agent => !CoopHideoutMissionLogic.IsPlayerHero(agent)).ToArray();
            var defenders = humans.Where(agent => agent.Team?.Side == BattleSideEnum.Defender).ToArray();
            // Used and Enabled are this machine's copy of the fire; Called is only set on the battle host, where the call troops request runs.
            var fires = mission.ActiveMissionObjects.FindAllWithType<StealthAreaUsePoint>()
                .Select(fire => new
                {
                    Id = fire.Id.Id,
                    Used = fire._isAlreadyUsed,
                    Enabled = fire._isEnabled,
                    Called = areas.IsReinforcementCalled(fire),
                })
                .ToArray();
            var status = new
            {
                Phase = native.Phase.ToString(),
                BattleHost = logic.IsAuthority,
                Fires = fires,
                PlayerHeroes = attackers.Count(CoopHideoutMissionLogic.IsPlayerHero),
                PlayerTroops = troops.Count(agent => agent.Origin?.BattleCombatant == PartyBase.MainParty),
                AttackerTroops = troops.Length,
                ActiveSentries = native.ActiveSentryCount,
                ActiveDefenders = defenders.Length,
                // Alarm state lives on the battle host; a peer's puppets don't carry it.
                AlarmedAgents = humans.Count(agent => agent.IsAlarmed()),
                FailCounterElapsed = mission.GetMissionBehavior<StealthFailCounterMissionLogic>().FailCounterElapsedTime,
            };
            return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(status));
        }
    }
}
