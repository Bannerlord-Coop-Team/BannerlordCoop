using ProtoBuf;

namespace MissionsShared
{
    [ProtoContract]
    public struct BoardGameMoveRequest
    {
        [ProtoMember(1)]
        public int FromIndex;

        [ProtoMember(2)]
        public int ToIndex;
    }
}
