using Scriban;
using System.IO;

namespace GameInterface.DynamicSync.Templates
{
    public class TemplateParser
    {
        private const string templateBasePath = "GameInterface.DynamicSync.Templates.";
        public static string Parse(string templateName, object model)
        {
            string templateContent;
            using (Stream stream = typeof(GameInterface).Assembly.GetManifestResourceStream($"{templateBasePath}{templateName}.txt") ?? throw new FileNotFoundException())
            using (StreamReader reader = new StreamReader(stream))
            {
                templateContent = reader.ReadToEnd();
            }
            var template = Template.Parse(templateContent);

            return template.Render(model, member => member.Name);
        }
    }
}
