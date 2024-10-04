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
        public PropertyInfo PropertyInfo;
        public BesiegerCamp BesiegerCamp;
        public object Value;

        public BesiegerCampPropertyChanged(PropertyInfo propertyInfo, BesiegerCamp instance, object value)
        {
            this.PropertyInfo = propertyInfo;
            this.BesiegerCamp = instance;
            this.Value = value;
        }
    }
}