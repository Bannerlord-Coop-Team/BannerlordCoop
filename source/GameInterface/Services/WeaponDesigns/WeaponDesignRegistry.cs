using GameInterface.Services.Registry;
using System.Threading;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemObjects
{
    internal class WeaponDesignRegistry : RegistryBase<WeaponDesign>
    {
        private const string ItemObjectIdPrefix = "CoopWeaponDesign";
        private static int InstanceCounter = 0;

        public WeaponDesignRegistry(IRegistryCollection collection) : base(collection) { }

        public override void RegisterAll()
        {
            //TODO
        }

        protected override string GetNewId(WeaponDesign obj)
        {
            return $"{ItemObjectIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
        }
    }
}
