﻿using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Library;

namespace Coop.Mod.Serializers
{
    [Serializable]
    public class NavigationPathSerializer : ICustomSerializer
    {
        int size;
        Vec2[] pathPoints;

        public NavigationPathSerializer(NavigationPath navigationPath)
        {
            size = navigationPath.Size;
            pathPoints = navigationPath.PathPoints;
        }

        public object Deserialize()
        {
            NavigationPath newNavigationPath = new NavigationPath();
            newNavigationPath.Size = size;

            // Assign pathpoints from serialized path points
            for(int i = 0; i < size; i++)
            {
                newNavigationPath.PathPoints[i] = pathPoints[i];
            }

            return newNavigationPath;
        }
    }
}