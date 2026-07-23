using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace E2E.Tests.Util.ObjectBuilders;

internal class TraitObjectBuilder : IObjectBuilder
{
    public object Build()
    {
        string stringid = "Trait Tests";
        return new TraitObject(stringid);
    }
}
