using Common.Commands;
using Newtonsoft.Json;
using SandBox.Objects.Usables;
using System;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Hideouts;

/// <summary>Live-test commands for the stealth hideout signal fire on this client.</summary>
internal static class HideoutSignalFireCommands
{
    private static CoopCommandResult Failed(string output, string errorCode) => new CoopCommandResult(false, output, errorCode);

    private static bool TryGetAmbush(out Mission mission, out CoopHideoutMissionLogic logic, out CoopHideoutAmbushController native)
    {
        mission = Mission.Current;
        logic = mission?.GetMissionBehavior<CoopHideoutMissionLogic>();
        native = logic?.Native as CoopHideoutAmbushController;
        return native != null;
    }

    /// <summary>Has this client's hero light the nearest signal fire the way the use key does.</summary>
    public sealed class LightSignalFireCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hideout";
        public string Name => "light_signal_fire";
        public string Description => "Has this client's hero light the nearest signal fire in a stealth hideout through native's use path.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetAmbush(out var mission, out _, out _))
                return Failed("Not in a co-op stealth hideout mission.", "mission_unavailable");
            var hero = mission.MainAgent;
            if (hero?.IsActive() != true)
                return Failed("This client's hero isn't on the field.", "hero_unavailable");
            var fire = mission.ActiveMissionObjects.FindAllWithType<StealthAreaUsePoint>()
                .OrderBy(point => point.GameEntity.GlobalPosition.DistanceSquared(hero.Position))
                .FirstOrDefault();
            if (fire == null)
                return Failed("This hideout has no signal fire.", "fire_unavailable");
            // The use key only offers the fire while native says it's usable, so this checks the same thing.
            if (!fire.IsUsableByAgent(hero))
                return Failed($"Signal fire {fire.Id.Id} can't be lit now (used={fire._isAlreadyUsed}, enabled={fire._isEnabled}, " +
                    $"in combat={fire.IsInCombat()}).", "fire_unusable");
            hero.HandleStartUsingAction(fire, -1);
            return new CoopCommandResult(true, $"Lit signal fire {fire.Id.Id}.");
        }
    }

    /// <summary>Prints the stealth phase, the signal fires, the player troops on the field and the sentries still standing.</summary>
    public sealed class StealthStatusCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.hideout";
        public string Name => "stealth_status";
        public string Description => "Prints this client's stealth hideout phase, signal fires, player troops and active sentries.";
        public CoopCommandSide Side => CoopCommandSide.Client;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetAmbush(out var mission, out var logic, out var native))
                return Failed("Not in a co-op stealth hideout mission.", "mission_unavailable");
            var status = new
            {
                Phase = native.Phase.ToString(),
                BattleHost = logic.IsAuthority,
                Fires = mission.ActiveMissionObjects.FindAllWithType<StealthAreaUsePoint>()
                    .Select(fire => new { Id = fire.Id.Id, Used = fire._isAlreadyUsed, Enabled = fire._isEnabled })
                    .ToArray(),
                PlayerTroops = mission.Agents.Count(agent => agent.IsActive() && agent.IsHuman && !agent.IsHero &&
                    agent.Team?.Side == BattleSideEnum.Attacker),
                PlayerHeroes = mission.Agents.Count(agent => agent.IsActive() && agent.IsHuman && agent.IsHero &&
                    agent.Team?.Side == BattleSideEnum.Attacker),
                ActiveSentries = native._stealthAreaData.Sum(area =>
                    area.StealthAreaMarkers.Values.Sum(sentries => sentries.Count(sentry => sentry.IsActive()))),
                ActiveDefenders = mission.Agents.Count(agent => agent.IsActive() && agent.IsHuman &&
                    agent.Team?.Side == BattleSideEnum.Defender),
            };
            return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(status));
        }
    }
}
