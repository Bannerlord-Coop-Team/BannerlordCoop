using GameInterface.DynamicSync.Templates;
using GameInterface.Services.ObjectManager;
using System;
using System.Reflection;

namespace GameInterface.DynamicSync.Builders;

public interface IDynamicSyncMemberBuilder
{
    string BuildPrefix(PropertyInfo propertyInfo);
    string BuildTranspiler(FieldInfo fieldInfo);
    string BuildHandler(FieldInfo fieldInfo);
    string BuildHandler(PropertyInfo propertyInfo);
    string BuildInternalMessage(FieldInfo fieldInfo);
    string BuildInternalMessage(PropertyInfo propertyInfo);
    string BuildNetworkMessage(FieldInfo fieldInfo);
    string BuildNetworkMessage(PropertyInfo propertyInfo);
}

public class DynamicSyncMemberBuilder : IDynamicSyncMemberBuilder
{
    private readonly DynamicSyncConfiguration config;
    private readonly IObjectManager objectManager;

    public DynamicSyncMemberBuilder(DynamicSyncConfiguration config, IObjectManager objectManager)
    {
        this.config = config;
        this.objectManager = objectManager;
    }

    public string BuildTranspiler(FieldInfo fieldInfo)
    {
        // TODO add to config
        return TemplateParser.Parse("Patches.TranspilerTemplate.txt",
            new
            {
                MemberDeclaringType = fieldInfo.DeclaringType.Name,
                MemberName = fieldInfo.Name,
                MemberType = fieldInfo.FieldType.Name
            });
    }

    public string BuildPrefix(PropertyInfo propertyInfo)
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

    public string BuildHandler(FieldInfo fieldInfo) => BuildHandler(fieldInfo.DeclaringType, fieldInfo.FieldType, fieldInfo);

    public string BuildHandler(PropertyInfo propertyInfo) => BuildHandler(propertyInfo.DeclaringType, propertyInfo.PropertyType, propertyInfo);

    private string BuildHandler(Type declaringType, Type memberType, MemberInfo memberInfo)
    {
        var template = objectManager.IsTypeManaged(memberType) switch
        {
            true => "Handlers.HandlerByNetworkIdTemplate.txt",
            _ => "Handlers.HandlerByValueTemplate.txt"
        };

        var handlerName = $"{declaringType.Name}_{memberInfo.Name}Handler";

        var handler = TemplateParser.Parse(template,
            new
            {
                HandlerName = handlerName,

                DeclaringType = declaringType.Name,

                InternalMessage = $"{declaringType.Name}_{memberInfo.Name}UpdatedMessage",
                NetworkMessage = $"NetworkSet_{declaringType.Name}_{memberInfo.Name}",

                MemberName = memberInfo.Name,

                Libraries = new string[]
                {
                    declaringType.Namespace,
                    memberType.Namespace
                }
            });

        DynamicSyncConfiguration.ExportFile($"{declaringType.Name}/Handlers/{handlerName}.cs", handler);

        return handler;
    }

    public string BuildInternalMessage(FieldInfo fieldInfo) => BuildInternalMessage(fieldInfo.DeclaringType, fieldInfo.FieldType, fieldInfo);
    public string BuildInternalMessage(PropertyInfo propertyInfo) => BuildInternalMessage(propertyInfo.DeclaringType, propertyInfo.PropertyType, propertyInfo);

    private string BuildInternalMessage(Type declaringType, Type memberType, MemberInfo memberInfo)
    {
        var messageName = $"{declaringType.Name}_{memberInfo.Name}UpdatedMessage";

        string internalMessage = TemplateParser.Parse(config.UpdatedMessageTemplate,
            new
            {
                MessageName = messageName,
                MemberDeclaringType = declaringType.Name,
                MemberName = memberInfo.Name,
                MemberType = memberType.Name,
                Libraries = new string[]
                {
                    declaringType.Namespace,
                    memberType.Namespace
                }
            });



        DynamicSyncConfiguration.ExportFile($"{declaringType.Name}/Messages/{messageName}.cs", internalMessage);

        return internalMessage;
    }

    public string BuildNetworkMessage(FieldInfo fieldInfo) => BuildNetworkMessage(fieldInfo.DeclaringType, fieldInfo.FieldType, fieldInfo);
    public string BuildNetworkMessage(PropertyInfo propertyInfo) => BuildNetworkMessage(propertyInfo.DeclaringType, propertyInfo.PropertyType, propertyInfo);

    public string BuildNetworkMessage(Type declaringType, Type memberType, MemberInfo memberInfo)
    {
        // TODO add to config
        var template = objectManager.IsTypeManaged(memberType) switch
        {
            true => "Messages.NetworkSetReferenceMessageTemplate.txt",
            _ => "Messages.NetworkSetValueMessageTemplate.txt"
        };

        var messageName = $"NetworkSet_{declaringType.Name}_{memberInfo.Name}";

        string networkMessage = TemplateParser.Parse(template,
            new
            {
                MessageName = messageName,
                MemberDeclaringType = declaringType.Name,
                MemberName = memberInfo.Name,
                MemberType = memberType.Name,
                Libraries = new string[]
                {
                    declaringType.Namespace,
                    memberType.Namespace
                }
            });

        DynamicSyncConfiguration.ExportFile($"{declaringType.Name}/Messages/{messageName}.cs", networkMessage);

        return networkMessage;
    }
}
