using GameInterface.DynamicSync.Templates;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace GameInterface.DynamicSync.Builders
{
    public class DynamicSyncPropertyBuilder
    {
        private readonly IObjectManager objectManager;

        public DynamicSyncPropertyBuilder(IObjectManager objectManager)
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
                    MemberType = propertyInfo.PropertyType.Name
                });

            return template;
        }

        public IEnumerable<string> GetMessages(PropertyInfo propertyInfo)
        {
            string localMessage = TemplateParser.Parse("Messages.LocalSetMessageTemplate",
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
            string networkMessage;
            if(objectManager.IsTypeManaged(propertyInfo.PropertyType))
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkSetReferenceMessageTemplate",
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
            else
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkSetValueMessageTemplate",
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

            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetLocalMessage.cs", localMessage);
            DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetNetworkMessage.cs", networkMessage);

            yield return localMessage;
            yield return networkMessage;
        }

        public string GetSubscription(PropertyInfo propertyInfo)
        {
            if (objectManager.IsTypeManaged(propertyInfo.PropertyType))
            {
                return TemplateParser.Parse("Handlers.SubscribeGenericSetReferenceTemplate",
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
            else
            {
                return TemplateParser.Parse("Handlers.SubscribeGenericSetValueTemplate",
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
    }
}
