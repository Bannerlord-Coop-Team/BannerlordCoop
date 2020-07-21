using System;
using TaleWorlds.Core;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class BannerSerializer : ICustomSerializer
    {
        private string data;

        public BannerSerializer(Banner value)
        {
            data = value.Serialize();
        }

        public object Deserialize()
        {
            Banner newBanner = new Banner(data);
            return newBanner;
        }
    }
}