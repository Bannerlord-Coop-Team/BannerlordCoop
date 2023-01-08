using Common.Messaging;
using SandBox.BoardGames;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.BoardGames.Messages
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
