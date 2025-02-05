using Common.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace E2E.Tests.Util.ObjectBuilders;
internal class CultureBuilder : IObjectBuilder
{
    public object Build()
    {
        var cultureObject = new CultureObject();

        cultureObject._defaultPolicyList = new MBList<PolicyObject>();
        cultureObject._maleNameList = new MBList<TextObject>();
        cultureObject._femaleNameList = new MBList<TextObject>();
        cultureObject._clanNameList = new MBList<TextObject>();
        cultureObject._cultureFeats = new MBList<FeatObject>();

        return cultureObject;
    }
}
