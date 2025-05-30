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
            return DynamicSyncUtils.GetSetTranspiler(fieldInfo);
        }

        public IEnumerable<string> GetMessages(FieldInfo fieldInfo)
        {
            var templateData = GetTemplateData(fieldInfo);
            string localMessage = DynamicSyncUtils.GetLocalSetMessage(fieldInfo);
            string networkMessage;
            if (objectManager.IsTypeManaged(fieldInfo.FieldType))
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkSetReferenceMessageTemplate", templateData);
            }
            else
            {
                networkMessage = TemplateParser.Parse("Messages.NetworkSetValueMessageTemplate", templateData);
            }

            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetLocalMessage.cs", localMessage);
            DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetNetworkMessage.cs", networkMessage);

            yield return localMessage;
            yield return networkMessage;
        }

        public string GetSubscription(FieldInfo fieldInfo)
        {
            var templateData = GetTemplateData(fieldInfo);
            if (objectManager.IsTypeManaged(fieldInfo.FieldType))
                return TemplateParser.Parse("Handlers.SubscribeSetReferenceTemplate", templateData);
            else
                return TemplateParser.Parse("Handlers.SubscribeSetValueTemplate", templateData);
        }

        private object GetTemplateData(FieldInfo fieldInfo)
        {
            return new
            {
                MemberDeclaringType = fieldInfo.DeclaringType.Name,
                MemberName = fieldInfo.Name,
                MemberType = fieldInfo.FieldType.Name,
                Libraries = DynamicSyncUtils.GetLibraries(fieldInfo)
            };
        }
    }
}
