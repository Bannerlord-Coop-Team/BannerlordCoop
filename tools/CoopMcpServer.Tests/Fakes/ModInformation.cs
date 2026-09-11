namespace Common;

public static class ModInformation
{
    public static bool IsServer { get; set; } = true;
    public static bool IsClient => !IsServer;
}
