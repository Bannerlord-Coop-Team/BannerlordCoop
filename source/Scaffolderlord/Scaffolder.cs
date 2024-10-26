using Microsoft.CodeAnalysis.Operations;
using Mono.TextTemplating;
using RazorLight;
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

namespace Scaffolderlord
{
	public class Scaffolder
	{
		private readonly RazorLightEngine engine;

		public Scaffolder()
		{
			engine = new RazorLightEngineBuilder()
				.DisableEncoding()
				.Build();
		}

		public async Task Generate(ITemplateModel templateModel)
			=> await Generate(templateModel.GetTemplateFilePath(), templateModel.GetOutputPath(), templateModel);


		public async Task Generate(string templatePath, string outputPath, object model)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			var safeOutputPath = CheckOutputPath(outputPath);
			var templateContent = ReadTemplateContent(templatePath);
			var renderedTemplateContent = await RenderTemplate(templateContent, model);
			await SaveFile(safeOutputPath, renderedTemplateContent);
			stopwatch.Stop();
			Console.WriteLine($"Created {safeOutputPath} ({stopwatch.ElapsedMilliseconds} ms)");
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

		private async Task<string> RenderTemplate(string templateContent, object model)
		{
			return await engine.CompileRenderStringAsync("templateKey", templateContent, model);
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
	}
}
