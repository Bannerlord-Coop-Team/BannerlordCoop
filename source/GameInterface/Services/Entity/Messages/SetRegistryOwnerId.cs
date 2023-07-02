using Common.Messaging;
using System;

namespace GameInterface.Services.Entity.Messages
{
    public record SetRegistryOwnerId : ICommand
    {
        public Guid OwnerId { get; set; }

        public SetRegistryOwnerId(Guid ownerId)
        {
            OwnerId = ownerId;
        }
    }
}
