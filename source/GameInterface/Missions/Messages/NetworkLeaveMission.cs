using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Missions.Services.Network.Messages
{
    /// <summary>
    /// Broadcast to P2P mesh peers when the local player deliberately leaves the mission/location, so
    /// they remove the player's agent immediately. The disconnect/timeout path remains the fallback for
    /// ungraceful exits (crash, network drop) where no message can be sent.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class NetworkLeaveMission : IEvent
    {
        [ProtoMember(1)]
        public readonly string ControllerId;

        public NetworkLeaveMission(string controllerId)
        {
            ControllerId = controllerId;
        }
    }
}
