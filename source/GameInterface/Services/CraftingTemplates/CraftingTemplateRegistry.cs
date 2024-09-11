using GameInterface.Services.Registry;
using System;
using TaleWorlds.Core;

namespace GameInterface.Services.CraftingTemplates
{
    internal class CraftingTemplateRegistry : RegistryBase<CraftingTemplate>
    {
        public CraftingTemplateRegistry(IRegistryCollection collection) : base(collection) { }

        public override void RegisterAll()
        {

        }

        protected override string GetNewId(CraftingTemplate craft)
        {
            return Guid.NewGuid().ToString();
        }
    }
}
