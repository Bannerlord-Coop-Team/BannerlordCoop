using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Services;

namespace GameInterface.CoopSessionData;

public interface ICoopSessionProvider : IGameAbstraction
{
    ICoopSession CoopSession { get; set; }
}

public class CoopSessionProvider : ICoopSessionProvider
{
    // Defaults to an empty session instead of null, otherwise a fresh campaign (no GameSaved/GameLoaded yet)
    // NREs the first time a joining client's data is read before the host's first save.
    public ICoopSession CoopSession { get; set; } = Save.Data.CoopSession.Empty;
}
