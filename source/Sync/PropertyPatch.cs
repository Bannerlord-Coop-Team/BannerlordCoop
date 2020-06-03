﻿using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;

namespace Sync
{
    /// <summary>
    ///     Patch generator for properties.
    /// </summary>
    public class PropertyPatch
    {
        private readonly Type m_Declaring;
        private readonly MethodPatch m_GetterPatch;
        private readonly MethodPatch m_SetterPatch;

        public PropertyPatch([NotNull] Type declaringType)
        {
            m_Declaring = declaringType;
            m_SetterPatch = new MethodPatch(m_Declaring);
            m_GetterPatch = new MethodPatch(m_Declaring);
        }

        public IEnumerable<MethodAccess> Setters => m_SetterPatch.Methods;
        public IEnumerable<MethodAccess> Getters => m_GetterPatch.Methods;

        public PropertyPatch RelaySetter([NotNull] PropertyInfo property)
        {
            m_SetterPatch.Relay(AccessTools.PropertySetter(m_Declaring, property.Name));
            return this;
        }

        public PropertyPatch RelaySetter(string sProperty)
        {
            m_SetterPatch.Relay(AccessTools.PropertySetter(m_Declaring, sProperty));
            return this;
        }

        public PropertyPatch RelayGetter([NotNull] PropertyInfo property)
        {
            m_SetterPatch.Relay(AccessTools.PropertyGetter(m_Declaring, property.Name));
            return this;
        }

        public PropertyPatch RelayGetter(string sProperty)
        {
            m_SetterPatch.Relay(AccessTools.PropertyGetter(m_Declaring, sProperty));
            return this;
        }
    }
}
