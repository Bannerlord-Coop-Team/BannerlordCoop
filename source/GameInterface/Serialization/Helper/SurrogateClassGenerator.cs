using GameInterface.Serialization.Collections;
using GameInterface.Serialization.Surrogates;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;

namespace GameInterface.Serialization.Helper
{
    public class SurrogateClassGenerator
    {
        public static BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        private static Dictionary<Type, string> Convertions = new Dictionary<Type, string>
        {
            { typeof(Boolean), "bool" },
            { typeof(Int32), "int" },
            { typeof(Single), "float" }
        };

        private static StringBuilder StringBuilder;

        private static List<FieldInfo> Fields;
        private static List<PropertyInfo> Properties;
        private static HashSet<string> Collections;
        private static string ClassName;
        private static string SurrogateType;

        public static string GenerateClass(Type type)
        {
            int tabs = 1;

            StringBuilder = new StringBuilder();
            Collections = new HashSet<string>();
            ClassName = $"{type.Name}Surrogate";
            SurrogateType = type.Name;

            GetFields(type);
            GetProperties(type);

            AppendLine($"[ProtoContract(SkipConstructor = true)]", tabs);
            AppendLine($"public readonly struct {ClassName}", tabs);
            AppendLine("{", tabs);

            GenerateClassProperties(tabs + 1);
            GenerateLookupTables(tabs + 1);
            GenerateClassConstructor(tabs + 1);
            GenerateClassDeserialize(tabs + 1);
            GenerateClassImplicits(tabs + 1);

            AppendLine("}", tabs);

            return StringBuilder.ToString();
        }

        private static void GenerateClassProperties(int tabs)
        {
            AppendLine($"#region Fields", tabs);

            int counter = 1;
            foreach(var field in Fields)
            {
                AppendLine($"[ProtoMember({counter++})]", tabs);

                string type = ConvertType(field.FieldType);

                AppendLine($"{type} {field.Name} {{ get; }}", tabs);
                StringBuilder.AppendLine();
            }

            AppendLine($"#endregion", tabs);

            AppendLine($"#region Properties", tabs);

            foreach (var property in Properties)
            {
                AppendLine($"[ProtoMember({counter++})]", tabs);

                string type = ConvertType(property.PropertyType);

                AppendLine($"{type} {property.Name} {{ get; }}", tabs);
                StringBuilder.AppendLine();
            }

            AppendLine($"#endregion", tabs);
        }

        private static void GenerateLookupTables(int tabs)
        {
            AppendLine($"#region Reflection", tabs);
            AppendLine($"private static readonly Dictionary<string, FieldInfo> Fields = new Dictionary<string, FieldInfo>", tabs);
            AppendLine("{", tabs);

            foreach(var field in Fields)
            {
                AppendLine($"{{ nameof({field.Name}), AccessTools.Field(typeof({SurrogateType}), nameof({field.Name})) }},", tabs + 1);
            }
            AppendLine("};", tabs);
            StringBuilder.AppendLine("");

            AppendLine($"private static readonly Dictionary<string, PropertyInfo> Properties = new Dictionary<string, PropertyInfo>", tabs);
            AppendLine("{", tabs);
            foreach (var property in Properties)
            {
                AppendLine($"{{ nameof({property.Name}),AccessTools.Property(typeof({SurrogateType}), nameof({property.Name})) }},", tabs + 1);
            }
            AppendLine("};", tabs);
            AppendLine($"#endregion", tabs);

            StringBuilder.AppendLine("");
        }

        private static void GenerateClassConstructor(int tabs)
        {
            AppendLine($"private {ClassName}({SurrogateType} obj)", tabs);
            AppendLine("{", tabs);

            //AppendLine("if (obj == null) return;", tabs + 1);

            foreach (var field in Fields)
            {
                if(Collections.Contains(field.Name) == false)
                {
                    string type = ConvertType(field.FieldType);
                    AppendLine($"{field.Name} = ({type})Fields[nameof({field.Name})].GetValue(obj);", tabs + 1);
                }
            }

            foreach (var property in Properties)
            {
                if (Collections.Contains(property.Name) == false)
                {
                    string type = ConvertType(property.PropertyType);
                    AppendLine($"{property.Name} = ({type})Properties[nameof({property.Name})].GetValue(obj);", tabs + 1);
                }
            }

            foreach (var field in Fields)
            {
                if (Collections.Contains(field.Name))
                {
                    GenerateCollectionSerializer(field.FieldType, tabs + 1, nameof(Fields));
                }
            }

            StringBuilder.AppendLine();

            foreach (var property in Properties)
            {
                if (Collections.Contains(property.Name))
                {
                    GenerateCollectionSerializer(property.PropertyType, tabs + 1, nameof(Properties));
                }
            }
            AppendLine("}", tabs);
            StringBuilder.AppendLine();
        }

        private static void GenerateCollectionSerializer(Type type, int tabs, string lookupTable)
        {
            Type genericType = type.GetGenericArguments().First();

            string serializerType = "";

            if(typeof(List<>).IsAssignableFrom(type))
            {
                serializerType = "List";
            }
            else if(typeof(Array).IsAssignableFrom(type))
            {
                serializerType = "Array";
            }

            string variableName = $"{type.Name}{serializerType}";
            AppendLine($"{serializerType}Serializer<{genericType.Name}> {variableName} = new {serializerType}Serializer<{genericType.Name}>();", tabs);
            AppendLine($"{variableName}.Pack(({serializerType}<{genericType.Name}>){lookupTable}[nameof({type.Name})].GetValue(obj));", tabs);
            AppendLine($"{type.Name} = {variableName}", tabs);
            StringBuilder.AppendLine();
        }

        private static void GenerateClassDeserialize(int tabs)
        {
            AppendLine($"private {SurrogateType} Deserialize()", tabs);
            AppendLine("{", tabs);
            AppendLine($"{SurrogateType} new{SurrogateType} = new {SurrogateType}();", tabs + 1);

            foreach (var field in Fields)
            {
                if (Collections.Contains(field.Name) == false)
                {
                    AppendLine($"Fields[nameof({field.Name})].SetValue(new{SurrogateType}, {field.Name});", tabs + 1);
                }
            }

            foreach (var property in Properties)
            {
                if (Collections.Contains(property.Name) == false)
                {
                    AppendLine($"Properties[nameof({property.Name})].SetValue(new{SurrogateType}, {property.Name});", tabs + 1);
                }
            }

            foreach (var field in Fields)
            {
                if (Collections.Contains(field.Name))
                {
                    AppendLine($"Fields[nameof({field.Name})].SetValue(new{SurrogateType}, {field.Name}.Unpack());", tabs + 1);
                }
            }

            StringBuilder.AppendLine();

            foreach (var property in Properties)
            {
                if (Collections.Contains(property.Name))
                {
                    AppendLine($"Properties[nameof({property.Name})].SetValue(new{SurrogateType}, {property.Name}.Unpack());", tabs + 1);
                }
            }

            AppendLine($"return new{SurrogateType};", tabs);
            AppendLine("}", tabs);
            StringBuilder.AppendLine();
        }

        private static void GenerateClassImplicits(int tabs)
        {
            AppendLine($"/// <summary>", tabs);
            AppendLine($"///     Prepare the serialization of the {SurrogateType} object from the game.", tabs);
            AppendLine($"/// </summary>", tabs);
            AppendLine($"/// <param name=\"{ClassName}\"></param>", tabs);
            AppendLine($"/// <returns></returns>", tabs);
            AppendLine($"public static implicit operator {ClassName}({SurrogateType} obj)", tabs);
            AppendLine("{", tabs);
            AppendLine($"return new {ClassName}(obj);", tabs + 1);
            AppendLine("}", tabs);

            AppendLine($"/// <summary>", tabs);
            AppendLine($"///     Retrieve the {SurrogateType} object from the surrogate.", tabs);
            AppendLine($"/// </summary>", tabs);
            AppendLine($"/// <param name=\"{ClassName}\">Surrogate object.</param>", tabs);
            AppendLine($"/// <returns>{SurrogateType} object.</returns>", tabs);
            AppendLine($"public static implicit operator {SurrogateType}({ClassName} surrogate)", tabs);
            AppendLine("{", tabs);
            AppendLine("return surrogate.Deserialize();", tabs + 1);
            AppendLine("}", tabs);
        }

        private static void AppendLine(string text, int tabs)
        {
            AppendTabs(tabs);
            StringBuilder.AppendLine(text);
        }

        private static void AppendTabs(int count)
        {
            for (int i = 0; i < count; i++)
            {
                StringBuilder.Append('\t');
            }
        }

        
        private static void GetFields(Type type)
        {
            Fields = type.GetFields(All).Where(f => f.Name.Contains("BackingField") == false).ToList();
        }

        private static void GetProperties(Type type)
        {
            HashSet<string> propertyNames = new HashSet<string>();

            foreach (var field in typeof(Hero).GetFields(All).Where(f => f.Name.Contains("BackingField")))
            {
                string propName = Regex.Match(field.Name, "<([A-Za-z1-9]+)>").Groups[1].Value;

                propertyNames.Add(propName);
            }

            Properties = type.GetProperties(All).Where(p => propertyNames.Contains(p.Name)).ToList();
        }

        private static string ConvertType(Type type)
        {
            if (Collections == null) throw new NullReferenceException($"{nameof(Collections)} was not initialized before calling {nameof(ConvertType)}");

            if(typeof(ICollection<>).IsAssignableFrom(type))
            {
                Collections.Add(type.Name);
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                return $"ListSerializer<{type.GenericTypeArguments.First()}>";
            }
            else if(type.IsArray)
            {
                return $"ArraySerializer<{type.GenericTypeArguments.First()}>";
            }

            if(Convertions.TryGetValue(type, out string output))
            {
                return output;
            }

            return type.Name;
        }

        private static string ConvertList(Type type)
        {
            if (type != typeof(List<>)) throw new InvalidOperationException($"Expected type List<T> but got {type}");

            Type genericArg = type.GenericTypeArguments.First();

            return $"ListSerializer<{genericArg.Name}>";
        }

        
    }
}
