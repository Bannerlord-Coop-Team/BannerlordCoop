using Missions.Battles;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Sessions;

/// <summary>
/// The deployment face of a campaign-free session: live and committed from the start, with no AI side effect.
/// The campaign coordinator never commits without a deployment controller, and its finish step would take
/// over enemy AI and charge leaderless troops, which belong to the mod in a session.
/// <c>BattleAuthorityMigrator</c> reads <see cref="IsActivated"/> to wake adopted AI after a host change.
/// </summary>
internal sealed class SessionDeploymentCoordinator : IBattleDeploymentCoordinator
{
    public bool IsActivated => true;

    public bool IsCommitted => true;

    public bool IsTeamSetupOver => Mission.Current != null;

    public bool ShouldWithhold(bool isOwnPartyTroop) => false;

    public bool OnLocalDeploymentFinished(Action replicateCommittedTroops = null) => false;

    public void OnPromotedToHost()
    {
    }

    public void CatchUpJoiner(string controllerId)
    {
    }

    public void OnMissionReady()
    {
    }

    public void Tick(float dt)
    {
    }

    public void Dispose()
    {
    }
}
