using GameInterface.Services.Registry;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.CraftingService
{
    internal class CraftingRegistry : RegistryBase<Crafting>
    {
        public CraftingRegistry(IRegistryCollection collection) : base(collection) { }

        public override void RegisterAll()
        {

        }

        protected override string GetNewId(Crafting craft)
        {
            return Guid.NewGuid().ToString();
        }
    }
}
