using Common.Messaging;
using SandBox.BoardGames.Pawns;

namespace GameInterface.Missions.BoardGames.Messages
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
