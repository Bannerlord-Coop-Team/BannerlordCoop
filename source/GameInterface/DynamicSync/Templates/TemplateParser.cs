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

            var path = $"{templateBasePath}{templateName}.txt";

            using (Stream stream = typeof(GameInterface).Assembly.GetManifestResourceStream(path) ?? throw new FileNotFoundException(path))
            using (StreamReader reader = new StreamReader(stream))
            {
                templateContent = reader.ReadToEnd();
            }
            var template = Template.Parse(templateContent);

            return template.Render(model, member => member.Name);
        }
    }
}
