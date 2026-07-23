using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace E2E.Tests.Util.ObjectBuilders;

internal class PropertyOwnerBuilder : IObjectBuilder
{
    public object Build()
    {
        return new PropertyOwner<TraitObject>();
    }
}