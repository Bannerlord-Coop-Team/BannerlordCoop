namespace Coop.Mod
{
    public static class Coop
    {
        public static bool IsServer => CoopServer.Instance.Current != null;
        public static bool IsClientPlaying => CoopClient.Instance.ClientPlaying;
        public static bool IsClientReqWorldData => CoopClient.Instance.ClientRequestingWorldData;
    }
}
