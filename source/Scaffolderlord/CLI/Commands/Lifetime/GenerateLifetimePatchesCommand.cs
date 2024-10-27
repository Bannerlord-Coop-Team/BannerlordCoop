using DotMake.CommandLine;
using Scaffolderlord.Helpers;
using Scaffolderlord.Models;
using Scaffolderlord.Models.Lifetime;
using Scaffolderlord.Services;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scaffolderlord.CLI.Commands
{
	[CliCommand(
		Name = "lifetime-patches",
		Description = "Generates lifetime patches",
		Parent = typeof(RootCliCommand)
		)]
	public class GenerateLifetimePatchesCommand : GenerateCommandBase<LifetimePatchesTemplateModel>
	{
		public GenerateLifetimePatchesCommand(IScaffoldingService scaffoldingService) : base(scaffoldingService)
		{
		}
	}
}
