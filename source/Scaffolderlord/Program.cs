using Scaffolderlord.Models;
using System;
using static Scaffolderlord.Extensions;

namespace Scaffolderlord
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var typeInfo = ReflectionHelper.GetServiceTypeInfo("TaleWorlds.CampaignSystem.Settlements.Fief, TaleWorlds.CampaignSystem");
            var registryTemplateModel = new RegistryTemplateModel(typeInfo);

            var scaffolder = new Scaffolder();
            await scaffolder.Generate(registryTemplateModel);
        }
    }
}