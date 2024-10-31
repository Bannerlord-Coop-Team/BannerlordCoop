using Mono.TextTemplating;
using Scaffolderlord.Exceptions;
using Scaffolderlord.Models;
using static Scaffolderlord.Extensions;

namespace Scaffolderlord.Services.Impl
{
    // Implementation using mono.text templating and T4
    // If anyone developing new templates prefers to use T4 over Razor you can just use this service instead
    public class T4Scaffolder : IScaffoldingService
    {
        private readonly TemplateSettings TemplateSettings;

        public T4Scaffolder()
        {
            TemplateSettings = new TemplateSettings();
        }

        public async Task Generate(ITemplateModel templateModel)
            => await Generate(templateModel.GetTemplateFilePath(), templateModel.GetOutputPath(), templateModel);

        public async Task Generate(string templatePath, string outputPath, ITemplateModel model)
        {
            var generator = new Mono.TextTemplating.TemplateGenerator();

            var session = generator.GetOrCreateSession();
            session.Add("Model", model);

            var safeOutputPath = CheckOutputPath(outputPath);
            await generator.ProcessTemplateAsync(templatePath, safeOutputPath);

            foreach (var err in generator.Errors)
            {
                Console.WriteLine(err.ToString());
            }

            if (!generator.Errors.HasErrors)
            {
                Console.WriteLine($"{Path.GetFileName(safeOutputPath)} Generated!");
            }
        }

        private string CheckOutputPath(string outputPath)
        {
            string dir = Path.GetDirectoryName(outputPath)!;
            string fileName = Path.GetFileName(outputPath);
            if (!Directory.Exists(dir))
            {
                Console.WriteLine($"Creating directory: {dir}");
                Directory.CreateDirectory(dir);
            }

            if (File.Exists(outputPath))
            {
                if (GlobalOptions.OverrideExistingFiles)
                {
                    Console.WriteLine($"Warning: output file {fileName} already exists and will be overwritten!");
                }
                else
                {
                    Console.WriteLine($"Warning: output file {fileName} already exists, adding a suffix to avoid overwrite!");
                    return GetUniqueFilePath(outputPath);
                }
            }
            return outputPath;
        }

        private void ValidateTemplateModel(ITemplateModel templateModel)
        {
            if (!File.Exists(templateModel.GetTemplateFilePath())) throw new TemplateFileNotFoundException(templateModel.GetTemplateFilePath());
        }
    }
}
