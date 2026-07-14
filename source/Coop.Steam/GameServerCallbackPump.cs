using Common;
using System;

namespace Coop.Steam;

/// <summary>
/// Pumps the game server's Steam callbacks each game frame. The standalone server has no game
/// frame of its own the way the engine's user-flavor SteamAPI.RunCallbacks does, so the logon
/// and tunnel-accept callbacks only dispatch while this is ticked from the update loop.
/// </summary>
public class GameServerCallbackPump : IUpdateable
{
    public int Priority => UpdatePriority.MainLoop.SteamCallbacks;

    public void Update(TimeSpan frameTime) => SteamGameServerBoot.RunCallbacks();
}
