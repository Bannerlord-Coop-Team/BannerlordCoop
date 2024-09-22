using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.Json;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.BesiegerCamps.Extensions
{
    public static class BesiegerCampExtensions
    {
        public static string TryGetId(object value, ILogger logger)
        {
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            {
                logger.Error("Unable to resolve {type}", typeof(IObjectManager).FullName);
                return null;
            }

            if (!objectManager.TryGetId(value, out string typeId))
            {
                logger.Error("Unable to get ID for instance of type {type}", value.GetType().Name);
                return null;
            }

            return typeId;
        }

        // quick and dirty way to pass the type as refference isntead of type arg
        public static bool TryGetObject(this IObjectManager src, string id, Type type, out object obj)
        {
            MethodInfo method = src.GetType().GetMethod(nameof(IObjectManager.TryGetObject));
            MethodInfo genericMethod = method.MakeGenericMethod(type);

            object[] parameters = new object[] { id, null };
            bool result = (bool)genericMethod.Invoke(src, parameters);

            obj = parameters[1];

            return result;
        }

    }
}
