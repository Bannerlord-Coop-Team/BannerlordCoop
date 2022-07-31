using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Missions.Messages.BoardGames
{
    [ProtoContract]
    public readonly struct BoardGameChallengeResponse
    {
        public BoardGameChallengeResponse(Guid requestingPlayer, Guid targetPlayer, bool accepted)
        {
            RequestingPlayer = requestingPlayer;
            TargetPlayer = targetPlayer;
            Accepted = accepted;
        }
        [ProtoMember(1)]
        public Guid RequestingPlayer { get; }
        [ProtoMember(2)]
        public Guid TargetPlayer { get; }
        [ProtoMember(3)]
        public bool Accepted { get; }
    }
}
