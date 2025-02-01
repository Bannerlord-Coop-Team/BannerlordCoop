using Common.Messaging;
using TaleWorlds.Core;

namespace GameInterface.Services.Monsters.Messages
{
    internal class MonsterCreated : IEvent
    {
        public Monster Monster { get; }

        public MonsterCreated(Monster monster)
        {
            Monster = monster;
        }
    }
}
