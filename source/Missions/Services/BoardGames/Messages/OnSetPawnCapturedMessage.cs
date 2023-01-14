using Common.Messaging;
using SandBox.BoardGames.MissionLogics;
using SandBox.BoardGames.Pawns;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.BoardGames.Messages
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
