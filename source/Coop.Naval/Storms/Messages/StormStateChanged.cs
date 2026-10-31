using Common.Messaging;
using NavalDLC.Map;

namespace Coop.Naval.Storms.Messages;

internal readonly struct StormStateChanged : IEvent
{
    public readonly Storm Storm;
    public readonly bool IsNewStorm;

    public StormStateChanged(Storm storm, bool isNewStorm)
    {
        Storm = storm;
        IsNewStorm = isNewStorm;
    }
}
