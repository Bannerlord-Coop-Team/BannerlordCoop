using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Opens a coop field-battle mission. Implemented in the Missions assembly (which can reference the SandBox
/// mission behaviors and the coop P2P behaviors), and resolved from the shared container by the GameInterface
/// battle flow — GameInterface cannot reference Missions directly (Missions depends on GameInterface).
/// <para>
/// The implementation mirrors <c>SandBoxMissions.OpenBattleMission</c> but installs coop troop suppliers
/// (each client fields only the troops it owns), skips the deployment phase, and attaches the coop battle
/// behaviors — so it replaces <c>CampaignMission.OpenBattleMission</c> for coop battles.
/// </para>
/// </summary>
public interface ICoopFieldBattleLauncher
{
    /// <summary>[Client, game thread] Build and open the coop field battle for the player's current map event.</summary>
    Mission OpenCoopFieldBattle(MissionInitializerRecord rec);
}
