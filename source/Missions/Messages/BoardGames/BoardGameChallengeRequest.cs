using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Missions.Messages.BoardGames
{
    [ProtoContract]
    public readonly struct BoardGameChallengeRequest
    {
        public BoardGameChallengeRequest(Guid requestingPlayer, Guid targetPlayer)
        {
            RequestingPlayer = requestingPlayer;
            TargetPlayer = targetPlayer;
        }

        [ProtoMember(1)]
        public Guid RequestingPlayer { get; }
        [ProtoMember(2)]
        public Guid TargetPlayer { get; }
    }
}
