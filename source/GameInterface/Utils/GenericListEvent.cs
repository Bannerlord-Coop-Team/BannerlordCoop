using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Utils
{
    [ProtoContract(SkipConstructor = true)]
    public record GenericListEvent<TInstance, TValue> : IEvent
    {
        public GenericListEvent(TInstance instance, TValue value)
        {
            Instance = instance;
            Value = value;
        }

        public TInstance Instance { get; }
        public TValue Value { get; }
    }
}
