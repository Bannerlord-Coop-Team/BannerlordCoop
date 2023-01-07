using Common.Messaging;
using SandBox.BoardGames.MissionLogics;
using SandBox.BoardGames.Pawns;
using TaleWorlds.MountAndBlade;

namespace Missions.Messages.Agents
{
    public readonly struct OnSetPawnCapturedMessage : IEvent
    {
        public PawnBase pawn { get; }

        public OnSetPawnCapturedMessage(PawnBase _pawn)
        {
            pawn = _pawn;
        }
    }
}