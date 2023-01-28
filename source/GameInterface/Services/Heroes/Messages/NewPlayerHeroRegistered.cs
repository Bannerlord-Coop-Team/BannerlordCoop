using Common.Messaging;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Heroes.Interfaces
{
    public readonly struct NewPlayerHeroRegistered : IEvent
    {
        public uint GUID { get; }

        public NewPlayerHeroRegistered(MBGUID guid)
        {
            GUID = guid.InternalValue;
        }

        public NewPlayerHeroRegistered(uint guid)
        {
            GUID = guid;
        }
    }
}