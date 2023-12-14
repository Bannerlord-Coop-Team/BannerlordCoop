using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.PartyBases.Messages
{
    /// <summary>
    /// Sent to the client by the server when a PartyBase's ItemRoster is updated.
    /// </summary>
    public class NetworkItemRosterUpdated : IMessage
    {
        [ProtoMember(1)]
        public string PartyBaseId { get; }
        [ProtoMember(2)]
        public byte[] EquipmentElement { get; }
        [ProtoMember(3)]
        public int Number { get; }

        public NetworkItemRosterUpdated(string partyBaseId, byte[] equipmentElement, int number)
        {
            PartyBaseId = partyBaseId;
            EquipmentElement = equipmentElement;
            Number = number;
        }
    }
}
