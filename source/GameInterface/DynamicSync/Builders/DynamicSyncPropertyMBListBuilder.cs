using GameInterface.DynamicSync.Templates;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.DynamicSync.Builders
{
    public class DynamicSyncPropertyMBListBuilder
    {
        private readonly IObjectManager objectManager;

        public DynamicSyncPropertyMBListBuilder(IObjectManager objectManager)
        {
            this.objectManager = objectManager;
        }
        public string GetPrefix(PropertyInfo propertyInfo)
        {
            var template = TemplateParser.Parse("Patches.PropertySetPrefixTemplate",
                new
                {
                    MemberDeclaringType = propertyInfo.DeclaringType.Name,
                    MemberName = propertyInfo.Name,
                    MemberType = GetMbListTypeName(propertyInfo.PropertyType)
                });

            return template;
        }


        public string GetTranspiler(PropertyInfo propertyInfo)
        {
            string changeTemplate = TemplateParser.Parse("Patches.PropertyListChangeTranspilerTemplate",
                    new
                    {
                        MemberDeclaringType = propertyInfo.DeclaringType.Name,
                        MemberName = propertyInfo.Name,
                        MemberType = GetMbListTypeName(propertyInfo.PropertyType),
                        ElementType = GetElementType(propertyInfo.PropertyType).Name,
                        Libraries = new List<string>
                        {
                            propertyInfo.DeclaringType.Namespace,
                            propertyInfo.PropertyType.Namespace,
                            GetElementType(propertyInfo.PropertyType).Namespace
                        }
                    });

            return changeTemplate;
        }


        public IEnumerable<string> GetMessages(PropertyInfo propertyInfo)
        {
            string localMessage = TemplateParser.Parse("Messages.LocalSetMessageTemplate",
                new
                {
                    MemberDeclaringType = propertyInfo.DeclaringType.Name,
                    MemberName = propertyInfo.Name,
                    MemberType = GetMbListTypeName(propertyInfo.PropertyType),
                    Libraries = new List<string>
                    {
                        propertyInfo.DeclaringType.Namespace,
                        propertyInfo.PropertyType.Namespace,
                        GetElementType(propertyInfo.PropertyType).Namespace
                    }
                });

            string localAddMessage = TemplateParser.Parse("Messages.LocalCollectionAddMessageTemplate",
                new
                {
                    MemberDeclaringType = propertyInfo.DeclaringType.Name,
                    MemberName = propertyInfo.Name,
                    MemberType = GetMbListTypeName(propertyInfo.PropertyType),
                    ElementType = GetElementType(propertyInfo.PropertyType).Name,
                    Libraries = new List<string>
                    {
                        propertyInfo.DeclaringType.Namespace,
                        propertyInfo.PropertyType.Namespace,
                        GetElementType(propertyInfo.PropertyType).Namespace
                    }
                });

            string localRemoveMessage = TemplateParser.Parse("Messages.LocalCollectionRemoveMessageTemplate",
                new
                {
                    MemberDeclaringType = propertyInfo.DeclaringType.Name,
                    MemberName = propertyInfo.Name,
                    MemberType = GetMbListTypeName(propertyInfo.PropertyType),
                    ElementType = GetElementType(propertyInfo.PropertyType).Name,
                    Libraries = new List<string>
                    {
                        propertyInfo.DeclaringType.Namespace,
                        propertyInfo.PropertyType.Namespace,
                        GetElementType(propertyInfo.PropertyType).Namespace
                    }
                });

            string networkMessage;
            string networkAddMessage;
            string networkRemoveMessage;
            if (objectManager.IsTypeManaged(GetElementType(propertyInfo.PropertyType)))
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkCollectionSetReferenceMessageTemplate",
                    new
                    {
                        MemberDeclaringType = propertyInfo.DeclaringType.Name,
                        MemberName = propertyInfo.Name,
                        MemberType = GetMbListTypeName(propertyInfo.PropertyType),
                        Libraries = new List<string>
                        {
                        propertyInfo.DeclaringType.Namespace,
                        propertyInfo.PropertyType.Namespace,
                        GetElementType(propertyInfo.PropertyType).Namespace
                        }
                    });

                networkAddMessage = TemplateParser.Parse("Messages.NetworkCollectionAddReferenceMessageTemplate",
                    new
                    {
                        MemberDeclaringType = propertyInfo.DeclaringType.Name,
                        MemberName = propertyInfo.Name,
                        MemberType = GetMbListTypeName(propertyInfo.PropertyType),
                        ElementType = GetElementType(propertyInfo.PropertyType).Name,
                        Libraries = new List<string>
                        {
                            propertyInfo.DeclaringType.Namespace,
                            propertyInfo.PropertyType.Namespace,
                            GetElementType(propertyInfo.PropertyType).Namespace
                        }
                    });
                networkRemoveMessage = TemplateParser.Parse("Messages.NetworkCollectionRemoveReferenceMessageTemplate",
                    new
                    {
                        MemberDeclaringType = propertyInfo.DeclaringType.Name,
                        MemberName = propertyInfo.Name,
                        MemberType = GetMbListTypeName(propertyInfo.PropertyType),
                        ElementType = GetElementType(propertyInfo.PropertyType).Name,
                        Libraries = new List<string>
                        {
                            propertyInfo.DeclaringType.Namespace,
                            propertyInfo.PropertyType.Namespace,
                            GetElementType(propertyInfo.PropertyType).Namespace
                        }
                    });
            }
            else
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkCollectionSetValueMessageTemplate",
                    new
                    {
                        MemberDeclaringType = propertyInfo.DeclaringType.Name,
                        MemberName = propertyInfo.Name,
                        MemberType = GetMbListTypeName(propertyInfo.PropertyType),
                        ElementType = GetElementType(propertyInfo.PropertyType).Name,
                        Libraries = new List<string>
                        {
                            propertyInfo.DeclaringType.Namespace,
                            propertyInfo.PropertyType.Namespace,
                            GetElementType(propertyInfo.PropertyType).Namespace
                        }
                    });

                networkAddMessage = TemplateParser.Parse("Messages.NetworkCollectionAddValueMessageTemplate",
                    new
                    {
                        MemberDeclaringType = propertyInfo.DeclaringType.Name,
                        MemberName = propertyInfo.Name,
                        MemberType = GetMbListTypeName(propertyInfo.PropertyType),
                        ElementType = GetElementType(propertyInfo.PropertyType).Name,
                        Libraries = new List<string>
                        {
                            propertyInfo.DeclaringType.Namespace,
                            propertyInfo.PropertyType.Namespace,
                            GetElementType(propertyInfo.PropertyType).Namespace
                        }
                    });

                networkRemoveMessage = TemplateParser.Parse("Messages.NetworkCollectionRemoveValueMessageTemplate",
                    new
                    {
                        MemberDeclaringType = propertyInfo.DeclaringType.Name,
                        MemberName = propertyInfo.Name,
                        MemberType = GetMbListTypeName(propertyInfo.PropertyType),
                        ElementType = GetElementType(propertyInfo.PropertyType).Name,
                        Libraries = new List<string>
                        {
                            propertyInfo.DeclaringType.Namespace,
                            propertyInfo.PropertyType.Namespace,
                            GetElementType(propertyInfo.PropertyType).Namespace
                        }
                    });
            }

            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetLocalMessage.cs", localMessage);
            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetNetworkMessage.cs", networkMessage);

            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_AddLocalMessage.cs", localAddMessage);
            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_AddNetworkMessage.cs", networkAddMessage);

            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_RemoveLocalMessage.cs", localRemoveMessage);
            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_RemoveNetworkMessage.cs", networkRemoveMessage);

            yield return localMessage;
            yield return localAddMessage;
            yield return localRemoveMessage;
            yield return networkMessage;
            yield return networkAddMessage;
            yield return networkRemoveMessage;
        }

        public string GetSubscription(PropertyInfo propertyInfo)
        {
            if (objectManager.IsTypeManaged(GetElementType(propertyInfo.PropertyType)))
            {
                return TemplateParser.Parse("Handlers.SubscribeCollectionReferenceTemplate",
                    new
                    {
                        MemberDeclaringType = propertyInfo.DeclaringType.Name,
                        MemberName = propertyInfo.Name,
                        MemberType = GetMbListTypeName(propertyInfo.PropertyType),
                        ElementType = GetElementType(propertyInfo.PropertyType).Name,
                        Libraries = new List<string>
                        {
                            propertyInfo.DeclaringType.Namespace,
                            propertyInfo.PropertyType.Namespace
                        }
                    });
            }
            else
            {
                return TemplateParser.Parse("Handlers.SubscribeCollectionValueTemplate",
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
        private string GetMbListTypeName(Type type)
        {
            return $"MBList<{type.GetGenericArguments()[0].Name}>";
        }

        private Type GetElementType(Type type)
        {
            return type.GetGenericArguments()[0];
        }
    }
}
