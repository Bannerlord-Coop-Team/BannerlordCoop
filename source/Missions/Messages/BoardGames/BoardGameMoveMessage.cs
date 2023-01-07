using Common.Messaging;
using SandBox.BoardGames;
using TaleWorlds.MountAndBlade;

namespace Missions.Messages.Agents
{
    public readonly struct BoardGameMoveMessage : IEvent
    {
        public Move Move { get; }

        public BoardGameMoveMessage(Move move)
        {
            Move = move;
        }
    }
}