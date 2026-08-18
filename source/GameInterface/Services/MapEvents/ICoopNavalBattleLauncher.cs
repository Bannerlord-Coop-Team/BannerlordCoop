using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Opens a coop naval battle with the native naval mission behaviors and coop troop ownership.
/// Implemented in the Missions assembly because GameInterface does not reference NavalDLC.
/// </summary>
public interface ICoopNavalBattleLauncher
{
    /// <summary>[Client, game thread] Open the naval mission for the player's current map event.</summary>
    Mission OpenCoopNavalBattle(MissionInitializerRecord rec);
}
