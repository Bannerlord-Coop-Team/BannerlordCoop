using GameInterface.Services.Villages.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Siege;

namespace Scaffolderlord
{
    public static class Extensions
    {
        public static string GetRelativePath(string subPath) => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, subPath);

        public static string GetTemplatePath(string fileName) => Path.Combine(GetRelativePath("Templates"), $"{fileName}.tt");

        public static IEnumerable<PropertyInfo> GetPropertiesWithSetters(Type type)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic);

            return properties
                .Where(prop => prop.CanWrite);
        }
    }

}
