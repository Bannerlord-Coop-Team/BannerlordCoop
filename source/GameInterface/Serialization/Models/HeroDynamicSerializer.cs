using GameInterface.Serialization.DynamicModel;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Serialization.Models
{
    public class HeroDynamicSerializer : IDynamicSerializer
    {
        public HeroDynamicSerializer(IDynamicModelGenerator modelGenerator)
        {
            var excluded = new string[]
            {
                "_mother",
                "_father"
            };
            
            modelGenerator.CreateDynamicSerializer<Hero>(excluded);
        }
    }
}