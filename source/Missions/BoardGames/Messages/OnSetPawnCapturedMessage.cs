using Common.Messaging;
using SandBox.BoardGames.Pawns;

namespace Missions.BoardGames.Messages
{
    public readonly struct OnSetPawnCapturedMessage : IEvent
    {
        public PawnBase Pawn { get; }

        public OnSetPawnCapturedMessage(PawnBase pawn)
        {
            Pawn = pawn;
        }
    }
}
