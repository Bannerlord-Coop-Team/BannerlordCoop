using Common.Messaging;
using SandBox.BoardGames;

namespace Missions.BoardGames.Messages
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
