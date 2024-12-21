using System.Diagnostics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace E2E.Tests.Util.ObjectBuilders;

public class CraftingBuilder : IObjectBuilder
{
    public object Build()
    {
        var culture = (CultureObject)new CultureBuilder().Build();
        
        var randomElement = CraftingTemplate.All.GetRandomElement<CraftingTemplate>();
        var name = new TextObject("{=uZhHh7pm}Crafted {CURR_TEMPLATE_NAME}");
        name.SetTextVariable("CURR_TEMPLATE_NAME", new TextObject("Yey"));
        var crafting = new Crafting(randomElement, culture, name);
        crafting.Init();
        crafting.Randomize();

        return crafting;
    }
}