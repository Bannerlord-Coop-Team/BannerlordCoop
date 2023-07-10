namespace GameInterface
{
    public static class ModInformation
    {
        public static bool IsServer { get; set; } = false;
        public static bool IsClient => !IsServer;
    }
}
