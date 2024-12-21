using System.Diagnostics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace E2E.Tests.Util.ObjectBuilders;

public class CraftingBuilder : IObjectBuilder
{
    public object Build()
    {
        var template = new CraftingTemplate()
        {
            Pieces = new List<CraftingPiece>(),
            BuildOrders = new []
            {
                new PieceData(CraftingPiece.PieceTypes.Blade, 0),
                new PieceData(CraftingPiece.PieceTypes.Guard, 1),
                new PieceData(CraftingPiece.PieceTypes.Handle, 2)
            }
        };
        var culture = new CultureObject();
        var name = new TextObject("{=uZhHh7pm}Crafted {CURR_TEMPLATE_NAME}");
        name.SetTextVariable("CURR_TEMPLATE_NAME", new TextObject("Yey"));
        var crafting = new Crafting(template, culture, name);
        crafting.Init();
        //crafting.Randomize();

        return crafting;
    }
}