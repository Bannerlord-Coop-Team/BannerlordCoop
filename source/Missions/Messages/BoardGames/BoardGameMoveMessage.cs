using Common.Messaging;
using SandBox.BoardGames;
using TaleWorlds.MountAndBlade;

namespace Missions.Messages.Agents
{
    public readonly struct BoardGameMoveMessage : IEvent
    {
        public Move move { get; }

        public BoardGameMoveMessage(Move inMove)
        {
            move = inMove;
        }
    }
}