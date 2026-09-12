namespace GameInterface.Services.UI;

internal static class TacticalUnitSymbolsSettings
{
    private static volatile bool hideTacticalUnitSymbols;

    public static bool HideTacticalUnitSymbols => hideTacticalUnitSymbols;

    public static void SetHideTacticalUnitSymbols(bool hideTacticalUnitSymbols)
    {
        TacticalUnitSymbolsSettings.hideTacticalUnitSymbols = hideTacticalUnitSymbols;
    }
}
