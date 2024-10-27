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
		Name = "lifetime-handler",
		Description = "Generates lifetime handler",
		Parent = typeof(RootCliCommand)
		)]
	public class GenerateLifetimeHandlerCommand : GenerateCommandBase<LifetimeHandlerTemplateModel>
	{
		public GenerateLifetimeHandlerCommand(IScaffoldingService scaffoldingService) : base(scaffoldingService)
		{
		}
	}
}
