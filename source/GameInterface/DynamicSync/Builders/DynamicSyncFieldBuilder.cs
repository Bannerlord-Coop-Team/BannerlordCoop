using GameInterface.DynamicSync.Templates;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.DynamicSync.Builders;

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
        string localMessage = TemplateParser.Parse("Messages.UpdatedMessageTemplate",
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

        var template = objectManager.IsTypeManaged(fieldInfo.FieldType) switch
        {
            true => "Messages.NetworkSetReferenceMessageTemplate",
            _ => "Messages.NetworkSetValueMessageTemplate"
        };

        string networkMessage = TemplateParser.Parse(template,
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

        DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetLocalMessage.cs", localMessage);
        DynamicSyncConfiguration.ExportFile($"{fieldInfo.DeclaringType.Name}/{fieldInfo.DeclaringType.Name}_{fieldInfo.Name}_SetNetworkMessage.cs", networkMessage);

        yield return localMessage;
        yield return networkMessage;
    }

    public string GetSubscription(FieldInfo fieldInfo)
    {
        var template = objectManager.IsTypeManaged(fieldInfo.FieldType) switch
        {
            true => "Handlers.SubscribeGenericSetReferenceTemplate",
            _ => "Handlers.SubscribeGenericSetValueTemplate"
        };

        return TemplateParser.Parse(template,
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
