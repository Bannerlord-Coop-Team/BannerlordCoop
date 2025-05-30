using GameInterface.DynamicSync.Templates;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.DynamicSync.Builders
{
    public class DynamicSyncFieldArrayBuilder
    {
        private readonly IObjectManager objectManager;

        public DynamicSyncFieldArrayBuilder(IObjectManager objectManager)
        {
            this.objectManager = objectManager;
        }

        public string GetTranspiler(FieldInfo fieldInfo)
        {
            string setTemplate = DynamicSyncUtils.GetSetTranspiler(fieldInfo);

            string changeTemplate = TemplateParser.Parse("Patches.FieldArrayChangeTranspilerTemplate",
                    new
                    {
                        MemberDeclaringType = fieldInfo.DeclaringType.Name,
                        MemberName = fieldInfo.Name,
                        MemberType = GetArrayType(fieldInfo.FieldType),
                        ElementType = fieldInfo.FieldType.GetElementType().Name,
                        Libraries = DynamicSyncUtils.GetLibraries(fieldInfo)
                    });

            return string.Join(Environment.NewLine, setTemplate, changeTemplate);
        }


        public IEnumerable<string> GetMessages(FieldInfo fieldInfo)
        {
            string localMessage = DynamicSyncUtils.GetLocalSetMessage(fieldInfo);

            string localChangeMessage = TemplateParser.Parse("Messages.LocalArrayChangeMessageTemplate",
                new
                {
                    MemberDeclaringType = fieldInfo.DeclaringType.Name,
                    MemberName = fieldInfo.Name,
                    MemberType = GetArrayType(fieldInfo.FieldType),
                    ElementType = fieldInfo.FieldType.GetElementType().Name,
                    Libraries = DynamicSyncUtils.GetLibraries(fieldInfo)
                });

            string networkMessage;
            string networkChangeMessage;
            if(objectManager.IsTypeManaged(fieldInfo.FieldType.GetElementType()))
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkArraySetReferenceMessageTemplate",
                    new
                    {
                        MemberDeclaringType = fieldInfo.DeclaringType.Name,
                        MemberName = fieldInfo.Name,
                        MemberType = GetArrayType(fieldInfo.FieldType),
                        Libraries = DynamicSyncUtils.GetLibraries(fieldInfo)
                    });

                networkChangeMessage = TemplateParser.Parse("Messages.NetworkArrayChangeReferenceMessageTemplate",
                    new
                    {
                        MemberDeclaringType = fieldInfo.DeclaringType.Name,
                        MemberName = fieldInfo.Name,
                        MemberType = GetArrayType(fieldInfo.FieldType),
                        ElementType = fieldInfo.FieldType.GetElementType().Name,
                        Libraries = DynamicSyncUtils.GetLibraries(fieldInfo)
                    });
            }
            else
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkArraySetValueMessageTemplate",
                    new
                    {
                        MemberDeclaringType = fieldInfo.DeclaringType.Name,
                        MemberName = fieldInfo.Name,
                        MemberType = GetArrayType(fieldInfo.FieldType),
                        ElementType = fieldInfo.FieldType.GetElementType().Name,
                        Libraries = DynamicSyncUtils.GetLibraries(fieldInfo)
                    });

                networkChangeMessage = TemplateParser.Parse("Messages.NetworkArrayChangeValueMessageTemplate",
                    new
                    {
                        MemberDeclaringType = fieldInfo.DeclaringType.Name,
                        MemberName = fieldInfo.Name,
                        MemberType = GetArrayType(fieldInfo.FieldType),
                        ElementType = fieldInfo.FieldType.GetElementType().Name,
                        Libraries = DynamicSyncUtils.GetLibraries(fieldInfo)
                    });
            }

            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetLocalMessage.cs", localMessage);
            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetNetworkMessage.cs", networkMessage);

            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_ArrayChangeLocalMessage.cs", localChangeMessage);
            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_ArrayChangeNetworkMessage.cs", networkChangeMessage);

            yield return localMessage;
            yield return localChangeMessage;
            yield return networkMessage;
            yield return networkChangeMessage;
        }

        public string GetSubscription(FieldInfo fieldInfo)
        {
            if (objectManager.IsTypeManaged(fieldInfo.FieldType.GetElementType()))
            {
                return TemplateParser.Parse("Handlers.SubscribeArrayReferenceTemplate",
                    new
                    {
                        MemberDeclaringType = fieldInfo.DeclaringType.Name,
                        MemberName = fieldInfo.Name,
                        MemberType = GetArrayType(fieldInfo.FieldType),
                        ElementType = fieldInfo.FieldType.GetElementType().Name,
                        Libraries = DynamicSyncUtils.GetLibraries(fieldInfo)
                    });
            }
            else
            {
                return TemplateParser.Parse("Handlers.SubscribeArrayValueTemplate",
                    new
                    {
                        MemberDeclaringType = fieldInfo.DeclaringType.Name,
                        MemberName = fieldInfo.Name,
                        MemberType = fieldInfo.FieldType.Name,
                        Libraries = DynamicSyncUtils.GetLibraries(fieldInfo)
                    });
            }
        }
        private string GetArrayType(Type type)
        {
            return type.GetElementType().Name + "[]";
        }
    }
}
