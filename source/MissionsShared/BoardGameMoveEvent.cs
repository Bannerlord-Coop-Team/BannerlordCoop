using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace MissionsShared
{
    [ProtoContract]
    public class BoardGameMoveEvent
    {
        [ProtoMember(1)]
        public int fromIndex { get; set; }

        [ProtoMember(2)]
        public int toTileX { get; set; }

        [ProtoMember(3)]
        public int toTileY { get; set; }
    }
}
