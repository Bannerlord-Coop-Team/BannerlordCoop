using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Towns.Messages.Collections
{
    [ProtoContract(SkipConstructor = true)]
    internal record NetworkWorkshopsChanged : ICommand
    {
        [ProtoMember(1)]
        public string TownId { get; }

        [ProtoMember(2)]
        public string WorkshopId { get; }

        [ProtoMember(3)]
        public int Index { get; }

        public NetworkWorkshopsChanged(string id, string workshopId, int index)
        {
            TownId = id;
            WorkshopId = workshopId;
            Index = index;
        }
    }
}
