using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Missions.Messages.BoardGames
{
    [ProtoContract]
    public readonly struct PawnCapturedMessage
    {
        public PawnCapturedMessage(Guid gameId, int index)
        {
            GameId = gameId;
            Index = index;
        }

        [ProtoMember(1)]
        public Guid GameId { get; }
        [ProtoMember(2)]
        public int Index { get; }
    }
}
