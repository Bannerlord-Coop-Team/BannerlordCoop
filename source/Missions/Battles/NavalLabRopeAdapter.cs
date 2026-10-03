#if DEBUG
using Missions.Messages;

namespace Missions.Battles;

public interface INavalRopeAdapter
{
    string RequestRope(NetworkNavalLabAction action);
    object InspectRopes();
    void ConfigureFinalRopes(System.Action<NetworkNavalLabRopeFinal> send);
    void AcceptFinalRopes(NetworkNavalLabRopeFinal value);
}
#endif
