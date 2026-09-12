using System;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.UI.PlayerNameplates;

public interface IPlayerNameplateControllerResolver : IDisposable
{
    bool TryGetControllerId(Agent agent, out string controllerId);
}
