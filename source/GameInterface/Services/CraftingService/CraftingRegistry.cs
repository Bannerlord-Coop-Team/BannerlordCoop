using GameInterface.Services.Registry;
using System.Threading;
using TaleWorlds.Core;

namespace GameInterface.Services.CraftingService
{
    internal class CraftingRegistry : RegistryBase<Crafting>
    {
        private const string CraftingIdPrefix = "CoopCrafting";
        private static int InstanceCounter = 0;

        public CraftingRegistry(IRegistryCollection collection) : base(collection) { }

        public override void RegisterAll()
        {
            //Not needed
        }

        protected override string GetNewId(Crafting craft)
        {
            return $"{CraftingIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
        }
    }
}
