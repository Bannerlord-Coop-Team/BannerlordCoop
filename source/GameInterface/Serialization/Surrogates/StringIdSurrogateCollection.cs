using GameInterface.Serialization.Dynamic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Serialization.Surrogates
{
    public interface IStringIdSurrogateCollection
    {
    }

    public class StringIdSurrogateCollection : IStringIdSurrogateCollection
    {
        public StringIdSurrogateCollection(IDynamicModelGenerator modelGenerator)
        {
            modelGenerator.AssignSurrogate<BasicCharacterObject, StringIdSurrogate<BasicCharacterObject>>();
            modelGenerator.AssignSurrogate<Town, StringIdSurrogate<Town>>();
            modelGenerator.AssignSurrogate<Settlement, StringIdSurrogate<Settlement>>();
            modelGenerator.AssignSurrogate<TraitObject, StringIdSurrogate<TraitObject>>();
            modelGenerator.AssignSurrogate<ItemCategory, StringIdSurrogate<ItemCategory>>();
            modelGenerator.AssignSurrogate<CraftingTemplate, StringIdSurrogate<CraftingTemplate>>();
        }
    }
}
