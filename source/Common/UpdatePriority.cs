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
            public const int SteamCallbacks = 5000;         // Dispatches the standalone server's game-server Steam callbacks, before anything consumes them.

            public const int GameThread = 2000;             // Processes the pending requests in the GameThread. This is processed in the main game loop AFTER the client updates are done

            public const int Fps = 900;                     // Periodic opt-in FPS reporting.
        }
    }
}