namespace Coop.Mod
{
    public static class Coop
    {
        public static bool DoSync => IsClient || IsServer;
        public static bool IsServer => CoopServer.Instance.Current != null;
        public static bool IsClient => CoopClient.Instance.Connected;

        /// <summary>
        ///     The arbiter is the game instance with authority over all clients.
        /// </summary>
        public static bool IsArbiter =>
            IsServer && IsClient; // The server currently runs in the hosts game session.
    }
}
