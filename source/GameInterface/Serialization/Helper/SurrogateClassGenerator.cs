using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;

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
        private static string SurrogateTypeName;
        private static Type SurrogateType;

        public static string GenerateClass(Type type)
        {
            int tabs = 1;

            StringBuilder = new StringBuilder();
            Collections = new HashSet<string>();
            ClassName = $"{type.Name}Surrogate";
            SurrogateTypeName = type.Name;
            SurrogateType = type;

            GetFields(type);
            GetProperties(type);

            AppendLine($"[ProtoContract(SkipConstructor = true)]", tabs);

            string generatedClassType;
            if(type.IsClass)
            {
                generatedClassType = "class";
            }
            else if(type.IsValueType && !type.IsEnum)
            {
                generatedClassType = "readonly struct";
            }
            else
            {
                throw new InvalidOperationException("Expected a type of struct or class, but got something else");
            }

            AppendLine($"public {generatedClassType} {ClassName}", tabs);
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
                AppendLine($"{{ nameof({field.Name}), AccessTools.Field(typeof({SurrogateTypeName}), nameof({field.Name})) }},", tabs + 1);
            }
            AppendLine("};", tabs);
            StringBuilder.AppendLine("");

            AppendLine($"private static readonly Dictionary<string, PropertyInfo> Properties = new Dictionary<string, PropertyInfo>", tabs);
            AppendLine("{", tabs);
            foreach (var property in Properties)
            {
                AppendLine($"{{ nameof({property.Name}),AccessTools.Property(typeof({SurrogateTypeName}), nameof({property.Name})) }},", tabs + 1);
            }
            AppendLine("};", tabs);
            AppendLine($"#endregion", tabs);

            StringBuilder.AppendLine("");
        }

        private static void GenerateClassConstructor(int tabs)
        {
            AppendLine($"private {ClassName}({SurrogateTypeName} obj)", tabs);
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
            AppendLine($"private {SurrogateTypeName} Deserialize()", tabs);
            AppendLine("{", tabs);
            AppendLine($"{SurrogateTypeName} new{SurrogateTypeName} = new {SurrogateTypeName}();", tabs + 1);

            foreach (var field in Fields)
            {
                if (Collections.Contains(field.Name) == false)
                {
                    AppendLine($"Fields[nameof({field.Name})].SetValue(new{SurrogateTypeName}, {field.Name});", tabs + 1);
                }
            }

            foreach (var property in Properties)
            {
                if (Collections.Contains(property.Name) == false)
                {
                    AppendLine($"Properties[nameof({property.Name})].SetValue(new{SurrogateTypeName}, {property.Name});", tabs + 1);
                }
            }

            foreach (var field in Fields)
            {
                if (Collections.Contains(field.Name))
                {
                    AppendLine($"Fields[nameof({field.Name})].SetValue(new{SurrogateTypeName}, {field.Name}.Unpack());", tabs + 1);
                }
            }

            StringBuilder.AppendLine();

            foreach (var property in Properties)
            {
                if (Collections.Contains(property.Name))
                {
                    AppendLine($"Properties[nameof({property.Name})].SetValue(new{SurrogateTypeName}, {property.Name}.Unpack());", tabs + 1);
                }
            }

            AppendLine($"return new{SurrogateTypeName};", tabs);
            AppendLine("}", tabs);
            StringBuilder.AppendLine();
        }

        private static void GenerateClassImplicits(int tabs)
        {
            AppendLine($"/// <summary>", tabs);
            AppendLine($"///     Prepare the serialization of the {SurrogateTypeName} object from the game.", tabs);
            AppendLine($"/// </summary>", tabs);
            AppendLine($"/// <param name=\"{ClassName}\"></param>", tabs);
            AppendLine($"/// <returns></returns>", tabs);
            AppendLine($"public static implicit operator {ClassName}({SurrogateTypeName} obj)", tabs);
            AppendLine("{", tabs);
            if(SurrogateType.IsClass)
            {
                AppendLine($"if (obj == null) return null;", tabs + 1);
            }
            AppendLine($"return new {ClassName}(obj);", tabs + 1);
            AppendLine("}", tabs);

            AppendLine($"/// <summary>", tabs);
            AppendLine($"///     Retrieve the {SurrogateTypeName} object from the surrogate.", tabs);
            AppendLine($"/// </summary>", tabs);
            AppendLine($"/// <param name=\"{ClassName}\">Surrogate object.</param>", tabs);
            AppendLine($"/// <returns>{SurrogateTypeName} object.</returns>", tabs);
            AppendLine($"public static implicit operator {SurrogateTypeName}({ClassName} surrogate)", tabs);
            AppendLine("{", tabs);
            if (SurrogateType.IsClass)
            {
                AppendLine($"if (surrogate == null) return null;", tabs + 1);
            }
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
