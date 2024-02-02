using Common.Messaging;
using ProtoBuf;
#nullable enable
namespace Coop.Core.Server.Services.Towns.Messages
{
    /// <summary>
    /// Server sends this data when a Town Changes Governor
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkChangeTownGovernor : IEvent
    {
        [ProtoMember(1)]
        public string TownId { get; }
        [ProtoMember(2)]
        public string? GovernorId { get; }

        public NetworkChangeTownGovernor(string townId, string? governorId)
        {
            TownId = townId;
            GovernorId = governorId;
        }
    }
}
