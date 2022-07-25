using Common.Messaging;

namespace Coop.Mod.Mission
{
    public class MissionManager
    {
        public CoopMission activeMissions;

        public static IMessageBroker MessageBroker { get; private set; }
    }
}
