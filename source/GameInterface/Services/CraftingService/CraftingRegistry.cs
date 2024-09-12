using GameInterface.Services.Registry;
using System;
using TaleWorlds.Core;

namespace GameInterface.Services.CraftingService
{
    internal class CraftingRegistry : RegistryBase<Crafting>
    {
        public CraftingRegistry(IRegistryCollection collection) : base(collection) { }

        public override void RegisterAll()
        {
            //Not needed
        }

        protected override string GetNewId(Crafting craft)
        {
            return Guid.NewGuid().ToString();
        }
    }
}
