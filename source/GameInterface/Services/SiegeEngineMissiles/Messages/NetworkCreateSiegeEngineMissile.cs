using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.SiegeEngineMissiles.Messages
{
    /// <summary>
    /// An event published to clients, commanding them to create SiegeEngineMissile.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkCreateSiegeEngineMissile : ICommand
    {
        [ProtoMember(1)]
        public string SiegeEngineMissileId { get; }

        public NetworkCreateSiegeEngineMissile(string siegeEngineMissileId)
        {
            SiegeEngineMissileId = siegeEngineMissileId;
        }
    }
}
