using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Opens a coop walls-assault siege mission. Implemented in the Missions assembly and resolved from the
/// shared container by the GameInterface battle flow, like <see cref="ICoopFieldBattleLauncher"/>.
/// <para>
/// The implementation mirrors <c>SandBoxMissions.OpenSiegeMissionWithDeployment</c> but installs coop
/// troop suppliers, the coop spawn handler and deployment controller, and attaches the coop battle
/// behaviors. There is no native fallback for sieges: the native path would size to whole-side counts
/// and never attach the P2P behaviors.
/// </para>
/// </summary>
public interface ICoopSiegeBattleLauncher
{
    /// <summary>[Client, game thread] Build and open the coop siege mission for the player's current map event.</summary>
    Mission OpenCoopSiegeBattle(MissionInitializerRecord rec, float[] wallHitPointRatios,
        List<MissionSiegeWeapon> attackerWeapons, List<MissionSiegeWeapon> defenderWeapons);
}
