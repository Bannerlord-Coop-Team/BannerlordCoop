using DotMake.CommandLine;
using Scaffolderlord.Helpers;
using Scaffolderlord.Models;
using Scaffolderlord.Services;
using static Scaffolderlord.Extensions;

namespace Scaffolderlord.CLI.Commands
{
	/// <summary>
	/// Base for all generate commands
	/// </summary>
	public abstract class GenerateCommandBase<TModel> where TModel : ITemplateModel
	{
		protected IScaffoldingService scaffolder;

		public GenerateCommandBase(IScaffoldingService scaffoldingService)
		{
			this.scaffolder = scaffoldingService;
		}

		[CliArgument(
	Description = "Specify the fully qualified name of the type using the format: '<namespace>.<type name>, <assembly name>'. Example: 'TaleWorlds.CampaignSystem.Siege.BesiegerCamp, TaleWorlds.CampaignSystem'\"",
	Required = true
	)]
		public string? TypeFullyQualifiedName { get; set; }

		[CliOption(
			Name = "--overwrite",
			Description = "When set to true will overwrite any existing files, when false a new file with a suffix is created"
			)]

		public bool OverwriteExistingFiles { get; set; } = false;

		private void SetGlobalOptions()
		{
			GlobalOptions.OverrideExistingFiles = this.OverwriteExistingFiles;
		}

		protected virtual ITemplateModel GetTemplateModel()
		{
			var typeInfo = ReflectionHelper.GetServiceTypeInfo(TypeFullyQualifiedName!);
			return CreateInstance<TModel>(typeInfo);
		}

		public virtual async Task RunAsync()
		{
			SetGlobalOptions();
			var model = GetTemplateModel();
			await scaffolder.Generate(model);
		}
	}
}
