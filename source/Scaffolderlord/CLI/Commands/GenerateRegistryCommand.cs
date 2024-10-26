using DotMake.CommandLine;
using Scaffolderlord.Helpers;
using Scaffolderlord.Models;
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
		Name = "registry",
		Description = "Generates a Registry class",
		Parent = typeof(RootCliCommand)
		)]
	public class GenerateRegistryCommand : GenerateCommandBase<RegistryTemplateModel>
	{
		public GenerateRegistryCommand(IScaffoldingService scaffoldingService) : base(scaffoldingService)
		{
		}
	}
}
