namespace Common
{
    /// <summary>
    ///     Priorities when processing a <see cref="UpdateableList"/>. Higher priorities get updated first. Note that
    ///     the priorities are only considered within one <see cref="UpdateableList"/>.
    /// </summary>
    public static class UpdatePriority
    {
        public static class MainLoop
        {
            public const int Update = 4000;                 // The update loop of the client.
            public const int PollNetwork = Update - 1;      // Polls all events received over the network connection.
            public const int RailGun = PollNetwork - 1;     // The update loop of the clientside RailGun instance.
            
            public const int GameLoopRunner = 2000;         // Processes the pending requests in the GameLoopRunner. This is processed in the main game loop AFTER the client updates are done
            
            public const int SyncBufferedFields = 1000;     // Processes pending synchronization of buffered field changes.
            
            public const int ApplyAuthoritativeMobilePartyState = 500; // Applies the known serverside state of all MobileParty instances to the local game state.
            
            public const int DebugUI = 1;                   // Update the Debug UI.
        }

        public static class ServerThread
        {
            public const int Update = 3000;                       // The update loop of the server.
            public const int PollNetwork = Update - 1;            // Polls all events received over the network connection.
            public const int RailGun = PollNetwork - 1;           // The update loop of the serverside RailGun instance.
            public const int ProcessBroadcasts = RailGun - 1;     // Processes pending broadcasts.
        }
    }
}