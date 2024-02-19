using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace E2E.Tests.Util.ObjectBuilders;
internal class CultureBuilder : IObjectBuilder
{
    public object Build()
    {
        var cultureObject = new CultureObject();

        AccessTools.Field(typeof(CultureObject), "_defaultPolicyList").SetValue(cultureObject, new MBList<PolicyObject>());
        AccessTools.Field(typeof(CultureObject), "_maleNameList").SetValue(cultureObject, new MBList<TextObject>());
        AccessTools.Field(typeof(CultureObject), "_femaleNameList").SetValue(cultureObject, new MBList<TextObject>());
        AccessTools.Field(typeof(CultureObject), "_clanNameList").SetValue(cultureObject, new MBList<TextObject>());
        AccessTools.Field(typeof(CultureObject), "_cultureFeats").SetValue(cultureObject, new MBList<FeatObject>());

        return cultureObject;
    }
}
