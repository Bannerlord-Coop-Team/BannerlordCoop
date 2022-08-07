using System;
using System.Reflection;
using Coop.Mod.Patch;
using HarmonyLib;

namespace GameInterface.Utils
{
    public static class ReflectionUtils
    {
        public static void InvokePrivateMethod(
            Type type,
            string name,
            object instance,
            object[] parameters = null)
        {
            MethodInfo moduleInfo = AccessTools.Method(type, name);
            if (moduleInfo == null)
            {
                throw new MethodNotFoundException(
                    $"Couldn't find method {type}::{name}. New game version?");
            }

            moduleInfo.Invoke(instance, parameters);
        }

        public static T InvokePrivateMethod<T>(
            Type type,
            string name,
            object instance,
            object[] parameters = null)
        {
            MethodInfo moduleInfo = AccessTools.Method(type, name);
            if (moduleInfo == null)
            {
                throw new MethodNotFoundException(
                    $"Couldn't find method {type}::{name}. New game version?");
            }

            object result = moduleInfo.Invoke(instance, parameters);
            if (result == null)
            {
                throw new MethodNotFoundException(
                    $"Couldn't call {type}::{name}. New game version?");
            }

            if (!(result is T))
            {
                throw new MethodNotFoundException($"Unexpected return type of {type}::{name}.");
            }

            return (T)result;
        }

        public static T GetPrivateField<T>(Type type, string name, object instance)
            where T : class
        {
            FieldInfo fieldInfo = AccessTools.Field(type, name);
            if (fieldInfo == null)
            {
                throw new FieldNotFoundException(
                    $"Couldn't find field {type}::{name}. New game version?");
            }

            T field = fieldInfo.GetValue(instance) as T;
            if (field == null)
            {
                throw new FieldNotFoundException(
                    $"Couldn't access field {type}::{name}. New game version?");
            }

            return field;
        }

        public static void SetPrivateField(
            Type type,
            string name,
            object instance,
            object value)
        {
            FieldInfo InMemDriver_data = AccessTools.Field(type, name);
            if (InMemDriver_data == null)
            {
                throw new FieldNotFoundException(
                    $"Couldn't find field {type}::{name}. Did the DLL change?");
            }

            InMemDriver_data.SetValue(instance, value);
        }
    }
}
