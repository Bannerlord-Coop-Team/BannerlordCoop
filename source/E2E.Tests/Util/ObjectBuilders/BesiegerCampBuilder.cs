using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace E2E.Tests.Util.ObjectBuilders;
internal class BesiegerCampBuilder : IObjectBuilder
{
    public object Build()
    {
        var siegeEvent = GameObjectCreator.CreateInitializedObject<SiegeEvent>();
        return new BesiegerCamp(siegeEvent);
    }
}
