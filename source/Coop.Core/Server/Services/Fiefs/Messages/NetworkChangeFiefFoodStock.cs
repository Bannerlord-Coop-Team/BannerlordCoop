using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Fiefs.Messages
{
    /// <summary>
    /// Server sends this data when a Fief Changes Food stock
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public class NetworkChangeFiefFoodStock : IEvent
    {
        [ProtoMember(1)]
        public string FiefId { get; }
        [ProtoMember(2)]
        public float FoodStockQuantity { get; }

        public NetworkChangeFiefFoodStock(string fiefId, float foodStockQuantity)
        {
            FiefId = fiefId;
            FoodStockQuantity = foodStockQuantity;
        }
    }
}
