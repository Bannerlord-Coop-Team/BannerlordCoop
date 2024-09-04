using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Fiefs.Messages;
using GameInterface.Services.Fiefs.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Fiefs.Handlers
{
    /// <summary>
    /// Handles FiefState Changes.
    /// </summary>
    public class FiefHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<FiefHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;

        public FiefHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;

            messageBroker.Subscribe<ChangeFiefFoodStock>(HandleChangeFiefFoodStock);

        }


        private void HandleChangeFiefFoodStock(MessagePayload<ChangeFiefFoodStock> payload)
        {
            var obj = payload.What;

            if (objectManager.TryGetObject(obj.FiefId, out Fief fief) == false)
            {
                Logger.Error("Unable to find Fief ({fiefId})", obj.FiefId);
                return;
            }

            FiefPatches.ChangeFiefFoodStock(fief, obj.FoodStockQuantity);
        }



        
        public void Dispose()
        {
            
            messageBroker.Unsubscribe<ChangeFiefFoodStock>(HandleChangeFiefFoodStock);
        }
    }
}
