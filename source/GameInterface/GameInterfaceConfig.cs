using System;

namespace GameInterface
{
    public interface IGameInterfaceConfig
    {
        public bool IsServer { get; }
        public bool IsClient { get; }

        public bool DisableAI { get; }
    }

    public class GameInterfaceConfig : IGameInterfaceConfig
    {
        public bool IsServer { get; set; }
        public bool IsClient => !IsServer;

        public bool DisableAI { get; set; } = false;
    }
}
