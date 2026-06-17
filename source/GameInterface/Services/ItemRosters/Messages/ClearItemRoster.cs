using Common.Messaging;

namespace GameInterface.Services.ItemRosters.Messages
{
    public class ClearItemRoster : ICommand
    {
        public string ItemRosterId { get; }

        public ClearItemRoster(string itemRosterId)
        {
            ItemRosterId = itemRosterId;
        }
    }
}
