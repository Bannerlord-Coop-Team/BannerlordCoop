using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using static System.Net.Mime.MediaTypeNames;
using static TaleWorlds.CampaignSystem.CampaignOptions;

namespace E2E.Tests.Util.ObjectBuilders
{
    internal class SiegeEngineTypeBuilder : IObjectBuilder
    {
        public object Build()
        {
            return new SiegeEngineType();
        }
    }
}