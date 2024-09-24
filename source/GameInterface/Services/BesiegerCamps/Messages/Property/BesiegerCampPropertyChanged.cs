using Common.Messaging;
using GameInterface.Services.BesiegerCampss.Messages;
using Serilog;
using System.Reflection;
using TaleWorlds.CampaignSystem.Siege;
using static Common.Serialization.BinaryFormatterSerializer;
using static GameInterface.Services.BesiegerCamps.Extensions.BesiegerCampExtensions;

namespace GameInterface.Services.BesiegerCamps.Messages
{
    public record BesiegerCampPropertyChanged : IEvent
    {
        public PropertyInfo propertyInfo;
        public BesiegerCamp besiegeCamp;
        public object value;

        public BesiegerCampPropertyChanged(PropertyInfo propertyInfo, BesiegerCamp instance, object value)
        {
            this.propertyInfo = propertyInfo;
            this.besiegeCamp = instance;
            this.value = value;
        }

        public NetworkBesiegerCampChangeProperty CreateNetworkMessage(ILogger logger)
        {
            string besiegeCampId = TryGetId(besiegeCamp, logger);
            PropertyInfo property = propertyInfo;
            bool isClass = property.PropertyType.IsClass;

            if (isClass)
            {
                var id = TryGetId(value, logger);
                return new NetworkBesiegerCampChangeProperty(property.Name, besiegeCampId, id);
            }
            else
            {
                var serializedValue = Serialize(value);
                return new NetworkBesiegerCampChangeProperty(property.Name, besiegeCampId, serializedValue);
            }
        }
    }
}