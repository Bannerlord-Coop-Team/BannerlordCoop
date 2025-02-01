using Common.Messaging;
using TaleWorlds.Core;

namespace GameInterface.Services.WeaponDesigns.Messages
{
    internal class WeaponDesignCreated : IEvent
    {
        public WeaponDesign WeaponDesign { get; }

        public WeaponDesignCreated(WeaponDesign weaponDesign)
        {
            WeaponDesign = weaponDesign;
        }
    }
}
