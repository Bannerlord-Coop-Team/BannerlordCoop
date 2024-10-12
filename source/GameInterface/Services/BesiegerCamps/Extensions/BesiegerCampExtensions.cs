using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.BesiegerCamps.Extensions
{
    public static class BesiegerCampExtensions
    {
        private static IObjectManager ResolveObjectManager(ILogger logger)
        {
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            {
                logger.Error("Unable to resolve {type}", typeof(IObjectManager).FullName);
                return null;
            }
            return objectManager;
        }

        public static bool TryGetId(object value, ILogger logger, out string id) => TryGetId(ResolveObjectManager(logger), value, logger, out id);

        public static bool TryGetId(this IObjectManager src, object value, ILogger logger, out string id)
        {
            id = null;
            if (value == null || src == null) return false;

            // temp fix for SiegeStrategy not being registered
            if (value is SiegeStrategy siegeStrategy)
            {
                id = siegeStrategy.StringId;
                return true;
            }

            if (!src.TryGetId(value, out id))
            {
                logger.Error("Unable to get ID for instance of type {type}", value.GetType().Name);
                return false;
            }

            return true;
        }
    }
}