using Common.Messaging;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemObjects.Messages
{
    internal class ItemObjectCreated : IEvent
    {
        public ItemObject ItemObject { get; }

        public ItemObjectCreated(ItemObject itemObject)
        {
            ItemObject = itemObject;
        }
    }
}
