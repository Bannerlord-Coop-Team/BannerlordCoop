namespace GameInterface.Configuration;

public class ModConfigProvider
{
    public static ModOptionsData ModOptions;

    public static void LoadModConfig(ModConfigData modConfigData)
    {
        ModOptions = modConfigData.ModOptions;
    }
}