using Mono.TextTemplating;
using Scaffolderlord.Exceptions;
using Scaffolderlord.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static Scaffolderlord.Extensions;

namespace Scaffolderlord
{
    public class Scaffolder
    {
        private readonly TemplateSettings TemplateSettings;

        public Scaffolder()
        {
            TemplateSettings = new TemplateSettings();
        }

        public async Task Generate(ITemplateModel templateModel)
            => await Generate(templateModel.GetTemplateFilePath(), templateModel.GetOutputPath(), templateModel);

        public async Task Generate(string templatePath, string outputPath, object model)
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
                if (Settings.OverrideExistingFiles)
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
            if (!IsValidPath(templateModel.GetOutputPath())) throw new InvalidOutputPathException(templateModel.GetOutputPath());
        }
    }
}
