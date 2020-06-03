using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;

namespace Sync
{
    public class PropertyPatcher
    {
        private readonly Type m_Declaring;
        private readonly MethodPatcher m_GetterPatcher;

        private readonly MethodPatcher m_SetterPatcher;
        // public IEnumerable<MethodAccess> Getters => m_Properties.Select(p => p.Getter);

        public PropertyPatcher([NotNull] Type declaringType)
        {
            m_Declaring = declaringType;
            m_SetterPatcher = new MethodPatcher(m_Declaring);
            m_GetterPatcher = new MethodPatcher(m_Declaring);
        }

        public IEnumerable<MethodAccess> Setters => m_SetterPatcher.Methods;
        public IEnumerable<MethodAccess> Getters => m_GetterPatcher.Methods;

        public PropertyPatcher Setter([NotNull] PropertyInfo property)
        {
            m_SetterPatcher.Patch(AccessTools.PropertySetter(m_Declaring, property.Name));
            return this;
        }

        public PropertyPatcher Setter(string sProperty)
        {
            m_SetterPatcher.Patch(AccessTools.PropertySetter(m_Declaring, sProperty));
            return this;
        }

        public PropertyPatcher Getter([NotNull] PropertyInfo property)
        {
            m_SetterPatcher.Patch(AccessTools.PropertyGetter(m_Declaring, property.Name));
            return this;
        }

        public PropertyPatcher Getter(string sProperty)
        {
            m_SetterPatcher.Patch(AccessTools.PropertyGetter(m_Declaring, sProperty));
            return this;
        }
    }
}
