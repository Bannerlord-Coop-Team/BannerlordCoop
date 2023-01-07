using Common.Messaging;
using SandBox.BoardGames;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Network.Messages.BoardGames
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
