using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Towns.Messages
{
    /// <summary>
    /// Server sends this data when a Town Changes Food stock
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class NetworkChangeTownFoodStock : IEvent
    {
        [ProtoMember(1)]
        public string TownId { get; }
        [ProtoMember(2)]
        public double FoodStockQuantity { get; }

        public NetworkChangeTownFoodStock(string townId, double foodStockQuantity)
        {
            TownId = townId;
            FoodStockQuantity = foodStockQuantity;
        }
    }
}
