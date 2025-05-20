using GameInterface.DynamicSync.Templates;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace GameInterface.DynamicSync.Builders
{
    public class DynamicSyncFieldBuilder
    {
        private readonly IObjectManager objectManager;

        public DynamicSyncFieldBuilder(IObjectManager objectManager)
        {
            this.objectManager = objectManager;
        }
        public string GetTranspiler(FieldInfo fieldInfo)
        {
            var template = TemplateParser.Parse("Patches.FieldSetTranspilerTemplate",
                new
                {
                    MemberDeclaringType = fieldInfo.DeclaringType.Name,
                    MemberName = fieldInfo.Name,
                    MemberType = fieldInfo.FieldType.Name
                });
            return template;
        }

        public IEnumerable<string> GetMessages(FieldInfo fieldInfo)
        {
            string localMessage = TemplateParser.Parse("Messages.LocalSetMessageTemplate",
                new
                {
                    MemberDeclaringType = fieldInfo.DeclaringType.Name,
                    MemberName = fieldInfo.Name,
                    MemberType = fieldInfo.FieldType.Name,
                    Libraries = new List<string>
                    {
                        fieldInfo.DeclaringType.Namespace,
                        fieldInfo.FieldType.Namespace
                    }
                });
            string networkMessage;
            if (objectManager.IsTypeManaged(fieldInfo.FieldType))
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkSetReferenceMessageTemplate",
                    new
                    {
                        MemberDeclaringType = fieldInfo.DeclaringType.Name,
                        MemberName = fieldInfo.Name,
                        MemberType = fieldInfo.FieldType.Name,
                        Libraries = new List<string>
                        {
                        fieldInfo.DeclaringType.Namespace,
                        fieldInfo.FieldType.Namespace
                        }
                    });
            }
            else
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkSetValueMessageTemplate",
                    new
                    {
                        MemberDeclaringType = fieldInfo.DeclaringType.Name,
                        MemberName = fieldInfo.Name,
                        MemberType = fieldInfo.FieldType.Name,
                        Libraries = new List<string>
                        {
                            fieldInfo.DeclaringType.Namespace,
                            fieldInfo.FieldType.Namespace
                        }
                    });
            }

            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetLocalMessage.cs", localMessage);
            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetNetworkMessage.cs", networkMessage);

            yield return localMessage;
            yield return networkMessage;
        }

        public string GetSubscription(FieldInfo fieldInfo)
        {
            if (objectManager.IsTypeManaged(fieldInfo.FieldType))
            {
                return TemplateParser.Parse("Handlers.SubscribeGenericSetReferenceTemplate",
                    new
                    {
                        MemberDeclaringType = fieldInfo.DeclaringType.Name,
                        MemberName = fieldInfo.Name,
                        MemberType = fieldInfo.FieldType.Name,
                        Libraries = new List<string>
                        {
                            fieldInfo.DeclaringType.Namespace,
                            fieldInfo.FieldType.Namespace
                        }
                    });
            }
            else
            {
                return TemplateParser.Parse("Handlers.SubscribeGenericSetValueTemplate",
                    new
                    {
                        MemberDeclaringType = fieldInfo.DeclaringType.Name,
                        MemberName = fieldInfo.Name,
                        MemberType = fieldInfo.FieldType.Name,
                        Libraries = new List<string>
                        {
                            fieldInfo.DeclaringType.Namespace,
                            fieldInfo.FieldType.Namespace
                        }
                    });
            }
        }
    }
}
