using Common.Messaging;

namespace GameInterface.Services.UI.Messages;

public readonly struct TacticalUnitSymbolsVisibilitySelected : IEvent
{
    public readonly bool HideTacticalUnitSymbols;

    public TacticalUnitSymbolsVisibilitySelected(bool hideTacticalUnitSymbols)
    {
        HideTacticalUnitSymbols = hideTacticalUnitSymbols;
    }
}
