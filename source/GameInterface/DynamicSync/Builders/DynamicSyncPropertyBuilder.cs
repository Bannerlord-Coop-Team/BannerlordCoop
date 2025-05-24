using GameInterface.DynamicSync.Templates;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using System.Reflection;

namespace GameInterface.DynamicSync.Builders;

public class DynamicSyncPropertyBuilder
{
    private readonly DynamicSyncConfiguration config;
    private readonly IObjectManager objectManager;

    public DynamicSyncPropertyBuilder(DynamicSyncConfiguration config, IObjectManager objectManager)
    {
        this.config = config;
        this.objectManager = objectManager;
    }
    public string GetPrefix(PropertyInfo propertyInfo)
    {
        // TODO move strings to config
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
        string localMessage = TemplateParser.Parse(config.UpdatedMessageTemplate,
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

        var template = objectManager.IsTypeManaged(propertyInfo.PropertyType) switch
        {
            true => config.NetworkSetReferenceMessageTemplate,
            _ => config.NetworkSetValueMessageTemplate
        };

        string networkMessage = TemplateParser.Parse(template,
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

        DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetLocalMessage.cs", localMessage);
        DynamicSyncConfiguration.ExportFile($"{propertyInfo.DeclaringType.Name}/{propertyInfo.DeclaringType.Name}_{propertyInfo.Name}_SetNetworkMessage.cs", networkMessage);

        yield return localMessage;
        yield return networkMessage;
    }

    public string GetSubscription(PropertyInfo propertyInfo)
    {
        // TODO move strings to config
        var template = objectManager.IsTypeManaged(propertyInfo.PropertyType) switch
        {
            true => "Handlers.SubscribeGenericSetReferenceTemplate",
            _ => "Handlers.SubscribeGenericSetValueTemplate"
        };

        return TemplateParser.Parse(template,
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
