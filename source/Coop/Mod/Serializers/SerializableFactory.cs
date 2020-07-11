using Sync.Store;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Serializers
{
    /// <summary>
    ///     Factory to create the serialization wrappers for non-serializable game objects.
    /// </summary>
    public class SerializableFactory : ISerializableFactory
    {
        public object Wrap(object obj)
        {
            switch (obj)
            {
                case TroopRoster troop:
                    return new TroopRosterSerializer(troop);
                default:
                    return obj;
            }
        }

        public object Unwrap(object obj)
        {
            switch (obj)
            {
                case TroopRosterSerializer ser:
                    return ser.Deserialize();
                default:
                    return obj;
            }
        }
    }
}
