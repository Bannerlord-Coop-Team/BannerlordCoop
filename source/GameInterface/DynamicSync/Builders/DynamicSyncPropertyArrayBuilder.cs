using GameInterface.DynamicSync.Templates;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.DynamicSync.Builders
{
    public class DynamicSyncPropertyArrayBuilder
    {
        private readonly IObjectManager objectManager;

        public DynamicSyncPropertyArrayBuilder(IObjectManager objectManager)
        {
            this.objectManager = objectManager;
        }
        public string GetPrefix(PropertyInfo propertyInfo) => DynamicSyncBuilderHelper.GetPrefix(propertyInfo);

        public string GetTranspiler(PropertyInfo propertyInfo)
        {
            return TemplateParser.Parse("Patches.PropertyArrayChangeTranspilerTemplate",
                    new
                    {
                        MemberDeclaringType = propertyInfo.DeclaringType.Name,
                        MemberName = propertyInfo.Name,
                        MemberType = GetArrayType(propertyInfo.PropertyType),
                        ElementType = propertyInfo.PropertyType.GetElementType().Name,
                        Libraries = new List<string>
                        {
                            propertyInfo.DeclaringType.Namespace,
                            propertyInfo.PropertyType.Namespace
                        }
                    });
        }


        public IEnumerable<string> GetMessages(PropertyInfo propertyInfo)
        {
            string localMessage = DynamicSyncBuilderHelper.GetLocalSetMessage(propertyInfo);

            string localChangeMessage = TemplateParser.Parse("Messages.LocalArrayChangeMessageTemplate",
                new
                {
                    MemberDeclaringType = propertyInfo.DeclaringType.Name,
                    MemberName = propertyInfo.Name,
                    MemberType = GetArrayType(propertyInfo.PropertyType),
                    ElementType = propertyInfo.PropertyType.GetElementType().Name,
                    Libraries = new List<string>
                    {
                        propertyInfo.DeclaringType.Namespace,
                        propertyInfo.PropertyType.Namespace
                    }
                });

            string networkMessage;
            string networkChangeMessage;
            if(objectManager.IsTypeManaged(propertyInfo.PropertyType.GetElementType()))
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkArraySetReferenceMessageTemplate",
                    new
                    {
                        MemberDeclaringType = propertyInfo.DeclaringType.Name,
                        MemberName = propertyInfo.Name,
                        MemberType = GetArrayType(propertyInfo.PropertyType),
                        Libraries = new List<string>
                        {
                        propertyInfo.DeclaringType.Namespace,
                        propertyInfo.PropertyType.GetElementType().Namespace
                        }
                    });

                networkChangeMessage = TemplateParser.Parse("Messages.NetworkArrayChangeReferenceMessageTemplate",
                    new
                    {
                        MemberDeclaringType = propertyInfo.DeclaringType.Name,
                        MemberName = propertyInfo.Name,
                        MemberType = GetArrayType(propertyInfo.PropertyType),
                        ElementType = propertyInfo.PropertyType.GetElementType().Name,
                        Libraries = new List<string>
                        {
                        propertyInfo.DeclaringType.Namespace,
                        propertyInfo.PropertyType.GetElementType().Namespace
                        }
                    });
            }
            else
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkArraySetValueMessageTemplate",
                    new
                    {
                        MemberDeclaringType = propertyInfo.DeclaringType.Name,
                        MemberName = propertyInfo.Name,
                        MemberType = GetArrayType(propertyInfo.PropertyType),
                        ElementType = propertyInfo.PropertyType.GetElementType().Name,
                        Libraries = new List<string>
                        {
                            propertyInfo.DeclaringType.Namespace,
                            propertyInfo.PropertyType.GetElementType().Namespace
                        }
                    });

                networkChangeMessage = TemplateParser.Parse("Messages.NetworkArrayChangeValueMessageTemplate",
                    new
                    {
                        MemberDeclaringType = propertyInfo.DeclaringType.Name,
                        MemberName = propertyInfo.Name,
                        MemberType = GetArrayType(propertyInfo.PropertyType),
                        ElementType = propertyInfo.PropertyType.GetElementType().Name,
                        Libraries = new List<string>
                        {
                        propertyInfo.DeclaringType.Namespace,
                        propertyInfo.PropertyType.GetElementType().Namespace
                        }
                    });
            }

            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetLocalMessage.cs", localMessage);
            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetNetworkMessage.cs", networkMessage);

            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_ArrayChangeLocalMessage.cs", localChangeMessage);
            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_ArrayChangeNetworkMessage.cs", networkChangeMessage);

            yield return localMessage;
            yield return localChangeMessage;
            yield return networkMessage;
            yield return networkChangeMessage;
        }

        public string GetSubscription(PropertyInfo propertyInfo)
        {
            if (objectManager.IsTypeManaged(propertyInfo.PropertyType.GetElementType()))
            {
                return TemplateParser.Parse("Handlers.SubscribeArrayReferenceTemplate",
                    new
                    {
                        MemberDeclaringType = propertyInfo.DeclaringType.Name,
                        MemberName = propertyInfo.Name,
                        MemberType = GetArrayType(propertyInfo.PropertyType),
                        ElementType = propertyInfo.PropertyType.GetElementType().Name,
                        Libraries = new List<string>
                        {
                            propertyInfo.DeclaringType.Namespace,
                            propertyInfo.PropertyType.Namespace
                        }
                    });
            }
            else
            {
                return TemplateParser.Parse("Handlers.SubscribeArrayValueTemplate",
                    new
                    {
                        MemberDeclaringType = propertyInfo.DeclaringType.Name,
                        MemberName = propertyInfo.Name,
                        MemberType = propertyInfo.PropertyType.Name,
                        Libraries = new List<string>
                        {
                            propertyInfo.DeclaringType.Namespace,
                            propertyInfo.PropertyType.Namespace
                        }
                    });
            }
        }
        private string GetArrayType(Type type)
        {
            return type.GetElementType().Name + "[]";
        }
    }
}
