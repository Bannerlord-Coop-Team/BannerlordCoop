using Common.Messaging;

namespace GameInterface.Services.ItemRosters.Messages
{
    public class ClearItemRoster : ICommand
    {
        public uint ItemRosterId { get; }

        public ClearItemRoster(uint itemRosterId)
        {
            ItemRosterId = itemRosterId;
        }
    }
}
