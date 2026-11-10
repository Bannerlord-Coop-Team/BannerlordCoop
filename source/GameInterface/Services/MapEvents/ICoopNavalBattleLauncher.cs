using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Opens a coop naval battle mission. Implemented in Missions.Naval, which is only loaded and registered while
/// NavalDLC is active, so resolving this interface also answers whether this client can open a naval battle.
/// </summary>
public interface ICoopNavalBattleLauncher
{
    /// <summary>[Client, game thread] Build and open the coop naval battle for the player's current map event.</summary>
    Mission OpenCoopNavalBattle(MissionInitializerRecord rec);
}
