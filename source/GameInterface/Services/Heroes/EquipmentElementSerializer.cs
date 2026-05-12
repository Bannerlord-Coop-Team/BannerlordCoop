using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using System.Text;

namespace GameInterface.Services.Heroes
{
    /// <summary>
    /// Custom serializer for <see cref="EquipmentElement"/>
    /// Each client has all ItemObject data loaded locally, so the full object
    /// can be reconstructed using StringId
    /// </summary>
    public static class EquipmentElementSerializer
    {
        /// <summary>
        /// Serializes an <see cref="EquipmentElement"/> by extracting the StringId
        /// of the contained <see cref="ItemObject"/> and converting it into bytes
        /// </summary>
        public static byte[] Serialize(EquipmentElement element)
        {
            var stringId = element.Item?.StringId ?? string.Empty;
            return Encoding.UTF8.GetBytes(stringId);
        }
        /// <summary>
        /// Deserializes an <see cref="EquipmentElement"/> by reconstructing the
        /// <see cref="ItemObject"/> from the StringId 
        /// </summary>
        public static EquipmentElement Deserialize(byte[] bytes)
        {
            var stringId = Encoding.UTF8.GetString(bytes);
            var item = MBObjectManager.Instance.GetObject<ItemObject>(stringId);
            return new EquipmentElement(item);
        }
    }
}