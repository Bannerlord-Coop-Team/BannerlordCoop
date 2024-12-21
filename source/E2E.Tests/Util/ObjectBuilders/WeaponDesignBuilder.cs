using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace E2E.Tests.Util.ObjectBuilders;

public class WeaponDesignBuilder : IObjectBuilder
{
    public object Build()
    {
        var cratingTemplate = new CraftingTemplate
        {
            BuildOrders = new []
            {
                new PieceData(CraftingPiece.PieceTypes.Blade, 0),
                new PieceData(CraftingPiece.PieceTypes.Guard, 1),
                new PieceData(CraftingPiece.PieceTypes.Handle, 2)
            }
        };
        var textObject = new TextObject("My cool weapon design");
        var usedPieces = new[]
        {
            new WeaponDesignElement(new CraftingPiece()),
            new WeaponDesignElement(new CraftingPiece()),
            new WeaponDesignElement(new CraftingPiece())
        };
        
        var weaponDesign = new WeaponDesign(cratingTemplate, textObject, usedPieces);
        return weaponDesign;
    }
}