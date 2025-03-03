using GameInterface.Registry;
using System.Threading;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Workshops
{
    internal class WorkshopTypeRegistry : RegistryBase<WorkshopType>
    {
        private const string WorkshopTypePrefix = $"Coop{nameof(WorkshopType)}";
        private int InstanceCounter = 0;

        public WorkshopTypeRegistry(IRegistryCollection collection) : base(collection) { }

        public override void RegisterAll()
        {
            // THIS BREAKS SAVING AND LOADING FOR SOME REASON, DO NOT UNCOMMENT UNTIL WE FIGURE OUT WHY AND HOW TO FIX
            //foreach(WorkshopType workshopType in WorkshopType.All)
            //{
            //    RegisterNewObject(workshopType, out var _);
            //}
        }

        protected override string GetNewId(WorkshopType party)
        {
            return WorkshopTypePrefix + Interlocked.Increment(ref InstanceCounter);
        }
    }
}
