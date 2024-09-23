using Common.Messaging;
using GameInterface.Services.SiegeEngineConstructionProgressss.Messages;
using Serilog;
using System;
using static Common.Serialization.BinaryFormatterSerializer;
using System.Reflection;
using static GameInterface.Services.BesiegerCamps.Extensions.BesiegerCampExtensions;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngineConstructionProgresss.Messages
{
    public record SiegeEngineConstructionProgressPropertyChanged : IEvent
    {
        public PropertyInfo propertyInfo;
        public SiegeEngineConstructionProgress siegeEngineConstructionProgress;
        public object value;

        public SiegeEngineConstructionProgressPropertyChanged(PropertyInfo propertyInfo, SiegeEngineConstructionProgress instance, object value)
        {
            this.propertyInfo = propertyInfo;
            this.siegeEngineConstructionProgress = instance;
            this.value = value;
        }

        public NetworkSiegeEngineConstructionProgressChangeProperty CreateNetworkMessage(ILogger logger)
        {
            string besiegeCampId = TryGetId(siegeEngineConstructionProgress, logger);
            PropertyInfo property = propertyInfo;
            bool isClass = property.PropertyType.IsClass;

            if (isClass)
            {
                var id = TryGetId(value, logger);
                return new NetworkSiegeEngineConstructionProgressChangeProperty(property.Name, besiegeCampId, id);
            }
            else
            {
                var serializedValue = Serialize(value);
                return new NetworkSiegeEngineConstructionProgressChangeProperty(property.Name, besiegeCampId, serializedValue);
            }
        }

    }
}
