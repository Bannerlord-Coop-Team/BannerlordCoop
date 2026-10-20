using Missions.Services.Network;

namespace Missions.Battles;

/// <summary>The per-battle parts of a <see cref="CoopBattleController"/> that battle-type services (the naval ship services) build on.</summary>
public record BattleContext(
    IBattleSession Session,
    IBattleDeploymentCoordinator Deployment,
    ICoopMissionComponent MissionComponent,
    IHostEpochPolicy HostEpochPolicy,
    IBattleResultCommitter ResultCommitter,
    IReinforcementFielder ReinforcementFielder,
    IMissionContext MissionContext,
    IBattleTroopWithdrawal TroopWithdrawal,
    IBattlePartyWithdrawal PartyWithdrawal);
