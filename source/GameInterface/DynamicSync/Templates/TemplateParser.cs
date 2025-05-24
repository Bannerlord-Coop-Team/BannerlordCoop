using Scriban;
using System.IO;
using System.Linq;
using System.Reflection;

namespace GameInterface.DynamicSync.Templates;

public class TemplateParser
{
    private static readonly Assembly _assembly = typeof(TemplateParser).Assembly;
    private const string TemplateBasePath = "GameInterface.DynamicSync.Templates.";

    public static string Parse(string templateName, object model)
    {
        var resourceName = TemplateBasePath + templateName;
        var templateContent = GetEmbeddedResourceContent(resourceName);
        var template = Template.Parse(templateContent);
        return template.Render(model, member => member.Name);
    }

    private static string GetEmbeddedResourceContent(string resourceName)
    {
        using var stream = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // Optional: Helper method to list all available embedded resources (useful for debugging)
    public static string[] GetAvailableTemplates()
    {
        return _assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(TemplateBasePath))
            .Select(name => name.Substring(TemplateBasePath.Length))
            .ToArray();
    }
}
