using GameInterface.DynamicSync.Templates;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.DynamicSync.Builders
{
    public class DynamicSyncFieldListBuilder
    {
        private readonly IObjectManager objectManager;

        public DynamicSyncFieldListBuilder(IObjectManager objectManager)
        {
            this.objectManager = objectManager;
        }

        public string GetTranspiler(FieldInfo fieldInfo)
        {
            string setTemplate = DynamicSyncBuilderHelper.GetSetTranspiler(fieldInfo);

            string changeTemplate = TemplateParser.Parse("Patches.FieldListChangeTranspilerTemplate",
                    new
                    {
                        MemberDeclaringType = fieldInfo.DeclaringType.Name,
                        MemberName = fieldInfo.Name,
                        MemberType = GetListTypeName(fieldInfo.FieldType),
                        ElementType = GetElementType(fieldInfo.FieldType).Name,
                        Libraries = new List<string>
                        {
                            fieldInfo.DeclaringType.Namespace,
                            fieldInfo.FieldType.Namespace,
                            GetElementType(fieldInfo.FieldType).Namespace
                        }
                    });

            return string.Join(Environment.NewLine, setTemplate, changeTemplate);
        }


        public IEnumerable<string> GetMessages(FieldInfo fieldInfo)
        {
            string localMessage = DynamicSyncBuilderHelper.GetLocalSetMessage(fieldInfo);

            string localAddMessage = TemplateParser.Parse("Messages.LocalCollectionAddMessageTemplate",
                new
                {
                    MemberDeclaringType = fieldInfo.DeclaringType.Name,
                    MemberName = fieldInfo.Name,
                    MemberType = GetListTypeName(fieldInfo.FieldType),
                    ElementType = GetElementType(fieldInfo.FieldType).Name,
                    Libraries = new List<string>
                    {
                        fieldInfo.DeclaringType.Namespace,
                        fieldInfo.FieldType.Namespace,
                        GetElementType(fieldInfo.FieldType).Namespace
                    }
                });

            string localRemoveMessage = TemplateParser.Parse("Messages.LocalCollectionRemoveMessageTemplate",
                new
                {
                    MemberDeclaringType = fieldInfo.DeclaringType.Name,
                    MemberName = fieldInfo.Name,
                    MemberType = GetListTypeName(fieldInfo.FieldType),
                    ElementType = GetElementType(fieldInfo.FieldType).Name,
                    Libraries = new List<string>
                    {
                        fieldInfo.DeclaringType.Namespace,
                        fieldInfo.FieldType.Namespace,
                        GetElementType(fieldInfo.FieldType).Namespace
                    }
                });

            string networkMessage;
            string networkAddMessage;
            string networkRemoveMessage;
            if (objectManager.IsTypeManaged(GetElementType(fieldInfo.FieldType)))
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkCollectionSetReferenceMessageTemplate",
                    new
                    {
                        MemberDeclaringType = fieldInfo.DeclaringType.Name,
                        MemberName = fieldInfo.Name,
                        MemberType = GetListTypeName(fieldInfo.FieldType),
                        Libraries = new List<string>
                        {
                        fieldInfo.DeclaringType.Namespace,
                        fieldInfo.FieldType.Namespace,
                        GetElementType(fieldInfo.FieldType).Namespace
                        }
                    });

                networkAddMessage = TemplateParser.Parse("Messages.NetworkCollectionAddReferenceMessageTemplate",
                    new
                    {
                        MemberDeclaringType = fieldInfo.DeclaringType.Name,
                        MemberName = fieldInfo.Name,
                        MemberType = GetListTypeName(fieldInfo.FieldType),
                        ElementType = GetElementType(fieldInfo.FieldType).Name,
                        Libraries = new List<string>
                        {
                            fieldInfo.DeclaringType.Namespace,
                            fieldInfo.FieldType.Namespace,
                            GetElementType(fieldInfo.FieldType).Namespace
                        }
                    });
                networkRemoveMessage = TemplateParser.Parse("Messages.NetworkCollectionRemoveReferenceMessageTemplate",
                    new
                    {
                        MemberDeclaringType = fieldInfo.DeclaringType.Name,
                        MemberName = fieldInfo.Name,
                        MemberType = GetListTypeName(fieldInfo.FieldType),
                        ElementType = GetElementType(fieldInfo.FieldType).Name,
                        Libraries = new List<string>
                        {
                            fieldInfo.DeclaringType.Namespace,
                            fieldInfo.FieldType.Namespace,
                            GetElementType(fieldInfo.FieldType).Namespace
                        }
                    });
            }
            else
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkCollectionSetValueMessageTemplate",
                    new
                    {
                        MemberDeclaringType = fieldInfo.DeclaringType.Name,
                        MemberName = fieldInfo.Name,
                        MemberType = GetListTypeName(fieldInfo.FieldType),
                        ElementType = GetElementType(fieldInfo.FieldType).Name,
                        Libraries = new List<string>
                        {
                            fieldInfo.DeclaringType.Namespace,
                            fieldInfo.FieldType.Namespace,
                            GetElementType(fieldInfo.FieldType).Namespace
                        }
                    });

                networkAddMessage = TemplateParser.Parse("Messages.NetworkCollectionAddValueMessageTemplate",
                    new
                    {
                        MemberDeclaringType = fieldInfo.DeclaringType.Name,
                        MemberName = fieldInfo.Name,
                        MemberType = GetListTypeName(fieldInfo.FieldType),
                        ElementType = GetElementType(fieldInfo.FieldType).Name,
                        Libraries = new List<string>
                        {
                            fieldInfo.DeclaringType.Namespace,
                            fieldInfo.FieldType.Namespace,
                            GetElementType(fieldInfo.FieldType).Namespace
                        }
                    });

                networkRemoveMessage = TemplateParser.Parse("Messages.NetworkCollectionRemoveValueMessageTemplate",
                    new
                    {
                        MemberDeclaringType = fieldInfo.DeclaringType.Name,
                        MemberName = fieldInfo.Name,
                        MemberType = GetListTypeName(fieldInfo.FieldType),
                        ElementType = GetElementType(fieldInfo.FieldType).Name,
                        Libraries = new List<string>
                        {
                            fieldInfo.DeclaringType.Namespace,
                            fieldInfo.FieldType.Namespace,
                            GetElementType(fieldInfo.FieldType).Namespace
                        }
                    });
            }

            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetLocalMessage.cs", localMessage);
            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetNetworkMessage.cs", networkMessage);

            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_AddLocalMessage.cs", localAddMessage);
            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_AddNetworkMessage.cs", networkAddMessage);

            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_RemoveLocalMessage.cs", localRemoveMessage);
            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_RemoveNetworkMessage.cs", networkRemoveMessage);

            yield return localMessage;
            yield return localAddMessage;
            yield return localRemoveMessage;
            yield return networkMessage;
            yield return networkAddMessage;
            yield return networkRemoveMessage;
        }

        public string GetSubscription(FieldInfo fieldInfo)
        {
            if (objectManager.IsTypeManaged(GetElementType(fieldInfo.FieldType)))
            {
                return TemplateParser.Parse("Handlers.SubscribeCollectionReferenceTemplate",
                    new
                    {
                        MemberDeclaringType = fieldInfo.DeclaringType.Name,
                        MemberName = fieldInfo.Name,
                        MemberType = GetListTypeName(fieldInfo.FieldType),
                        ElementType = GetElementType(fieldInfo.FieldType).Name,
                        Libraries = new List<string>
                        {
                            fieldInfo.DeclaringType.Namespace,
                            fieldInfo.FieldType.Namespace
                        }
                    });
            }
            else
            {
                return TemplateParser.Parse("Handlers.SubscribeCollectionValueTemplate",
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
        private string GetListTypeName(Type type)
        {
            return $"List<{type.GetGenericArguments()[0].Name}>";
        }

        private Type GetElementType(Type type)
        {
            return type.GetGenericArguments()[0];
        }
    }
}
