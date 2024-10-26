using Scaffolderlord.Models;
using System;
using System.Linq;

namespace Scaffolderlord.Services
{
	public interface IScaffoldingService
	{
		Task Generate(ITemplateModel templateModel);
		Task Generate(string templatePath, string outputPath, ITemplateModel model);
	}
}
