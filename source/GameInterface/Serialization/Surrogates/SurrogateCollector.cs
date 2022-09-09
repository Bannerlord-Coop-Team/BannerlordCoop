using GameInterface.Serialization.Dynamic;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using static TaleWorlds.CampaignSystem.Hero;

namespace GameInterface.Serialization.Surrogates
{
    public interface ISurrogateCollector
    {
    }

    public class SurrogateCollector : ISurrogateCollector
    {
        public SurrogateCollector(IDynamicModelGenerator modelGenerator)
        {
            modelGenerator.AssignSurrogate<CampaignTime, CampaignTimeSurrogate>();
            modelGenerator.AssignSurrogate<HeroLastSeenInformation, HeroLastSeenInformationSurrogate>();
            modelGenerator.AssignSurrogate<Vec3, Vec3Surrogate>();
            modelGenerator.AssignSurrogate<Vec2, Vec2Surrogate>();
        }
    }
}
