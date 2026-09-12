#if DEBUG
using Missions.Messages;

namespace Missions.Battles;

public interface INavalRopeAdapter
{
    string RequestRope(NetworkNavalLabAction action);
    object InspectRopes();
}
#endif
