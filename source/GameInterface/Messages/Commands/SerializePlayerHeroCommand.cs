using System;

namespace GameInterface.Messages.Commands
{
    public readonly struct SerializePlayerHeroCommand
    {
        public Guid Id { get; }
        public TimeSpan Timeout { get; }
    }
    public readonly struct SerializePlayerHeroResponse
    {
        public Guid Id { get; }
        public TimeSpan Timeout { get; }
    }
}
