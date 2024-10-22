using System;
using static Scaffolderlord.Extensions;

namespace Scaffolderlord
{
    class Program
    {
        static async Task Main(string[] args)
        {
            //var templatePath = GetTemplatePath("RegistryTemplate");
            //var output = GetRelativePath(@"Output\\test.tt");

            //var scaffolder = new Scaffolder(templatePath,"output.cs");
            //await scaffolder.Generate();

            var test = ReflectionHelper.GetServiceTypeInfo("TaleWorlds.CampaignSystem.Siege.BesiegerCamp, TaleWorlds.CampaignSystem",
                new[] { "NumberOfTroopsKilledOnSide", "SiegeEvent", "SiegeEngines", "SiegeStrategy" },
                new[] { "_leaderParty" }
                );
        }
    }
}