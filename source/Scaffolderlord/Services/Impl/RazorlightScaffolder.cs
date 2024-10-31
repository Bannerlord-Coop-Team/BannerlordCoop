using Microsoft.CodeAnalysis.Operations;
using Microsoft.Extensions.Logging;
using RazorLight;
using RazorLight.Internal;
using Scaffolderlord.Exceptions;
using Scaffolderlord.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Library;
using static Scaffolderlord.Extensions;

namespace Scaffolderlord.Services.Impl
{
    // Implementation using razor
    public class RazorlightScaffolder : IScaffoldingService
    {
        private readonly ILogger _logger;
        private readonly IRazorLightEngine _engine;

        public RazorlightScaffolder(IRazorLightEngine engine, ILogger<RazorlightScaffolder> logger)
        {
            _engine = engine;
            _logger = logger;
        }

        public async Task Generate(ITemplateModel templateModel)
            => await Generate(templateModel.GetTemplateFilePath(), templateModel.GetOutputPath(), templateModel);


        public async Task Generate(string templatePath, string outputPath, ITemplateModel model)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            var safeOutputPath = CheckOutputPath(outputPath);
            var templateContent = ReadTemplateContent(templatePath);
            var renderedTemplateContent = await RenderTemplate(templateContent, model);
            await SaveFile(safeOutputPath, renderedTemplateContent);
            stopwatch.Stop();
            _logger.LogInformation("Created {path} ({took} ms)", safeOutputPath, stopwatch.ElapsedMilliseconds);
            Console.WriteLine("");
        }

        private async Task SaveFile(string outputPath, string content)
        {
            try
            {
                await File.WriteAllTextAsync(outputPath, content);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to save file", ex);
            }
        }

        private async Task<string> RenderTemplate(string templateContent, ITemplateModel model)
        {
            try
            {
                return await _engine.CompileRenderStringAsync(model.TemplateFileName, templateContent, model);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to render template", ex);
            }
        }

        private string ReadTemplateContent(string templatePath)
        {
            if (!File.Exists(templatePath)) throw new TemplateFileNotFoundException(templatePath);
            return File.ReadAllText(templatePath);
        }

        private string CheckOutputPath(string outputPath)
        {
            string dir = Path.GetDirectoryName(outputPath)!;
            string fileName = Path.GetFileName(outputPath);
            if (!Directory.Exists(dir))
            {
                _logger.LogDebug($"Creating directory: {dir}");
                Directory.CreateDirectory(dir);
            }

            if (File.Exists(outputPath))
            {
                if (GlobalOptions.OverrideExistingFiles)
                {
                    _logger.LogWarning($"Output file {fileName} already exists and will be overwritten!");
                }
                else
                {
                    _logger.LogInformation($"Output file {fileName} already exists, adding a suffix to avoid overwrite");
                    return GetUniqueFilePath(outputPath);
                }
            }
            return outputPath;
        }
    }
}
