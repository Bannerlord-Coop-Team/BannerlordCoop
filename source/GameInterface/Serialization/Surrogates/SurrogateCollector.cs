using GameInterface.Serialization.Dynamic;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using static TaleWorlds.CampaignSystem.Hero;

namespace GameInterface.Serialization.Surrogates
{
    public interface ISurrogateCollector
    {
    }

    public class SurrogateCollector : ISurrogateCollector
    {
        private readonly IStringIdSurrogateCollection stringIdSurrogateCollection;
        public SurrogateCollector(IDynamicModelGenerator modelGenerator)
        {
            stringIdSurrogateCollection = new StringIdSurrogateCollection(modelGenerator);

            modelGenerator.AssignSurrogate<TextObject, TextObjectSurrogate>();
            modelGenerator.AssignSurrogate<MBCharacterSkills, MBCharacterSkillsSurrogate>();

            modelGenerator.AssignSurrogate<CampaignTime, CampaignTimeSurrogate>();
            modelGenerator.AssignSurrogate<HeroLastSeenInformation, HeroLastSeenInformationSurrogate>();
            
            // Library classes
            modelGenerator.AssignSurrogate<Vec3, Vec3Surrogate>();
            modelGenerator.AssignSurrogate<Vec2, Vec2Surrogate>();
            modelGenerator.AssignSurrogate<MatrixFrame, MatrixFrameSurrogate>();
            modelGenerator.AssignSurrogate<Mat3, Mat3Surrogate>();

            // TODO figure out
            modelGenerator.AssignSurrogate<IssueBase, IssueBaseSurrogate>();
            modelGenerator.AssignSurrogate<MobileParty, MobilePartySurrogate>();
            modelGenerator.AssignSurrogate<PartyBase, MobilePartySurrogate>();
        }
    }
}
