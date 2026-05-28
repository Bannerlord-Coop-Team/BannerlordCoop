using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Services;

namespace GameInterface.CoopSessionData;

public interface ICoopSessionProvider : IGameAbstraction
{
    ICoopSession CoopSession { get; set; }
}

public class CoopSessionProvider : ICoopSessionProvider
{
    public ICoopSession CoopSession { get; set;}
}
