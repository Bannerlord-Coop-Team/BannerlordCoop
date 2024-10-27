using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scaffolderlord.CLI;
using Scaffolderlord.Helpers;
using System;
using System.Linq;
using System.Reflection;

namespace Scaffolderlord
{
	public static class Extensions
	{
		public static string GetMainProjectPath(string subPath)
		{
			var bannerlordCoopDir = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).FullName;
			return Path.Combine(bannerlordCoopDir, subPath);
		}
		public static string GetRelativePath(string subPath) => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, subPath);
		public static string GetUniqueFilePath(string filePath)
		{
			string directory = Path.GetDirectoryName(filePath);
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
			string extension = Path.GetExtension(filePath);

			string uniqueFilePath = filePath;
			int counter = 1;

			while (File.Exists(uniqueFilePath))
			{
				uniqueFilePath = Path.Combine(directory, $"{fileNameWithoutExtension}({counter++}){extension}");
			}

			return uniqueFilePath;
		}
		public static IServiceCollection PropagateLogger(this IServiceCollection src)
		{
			var logger = src.BuildServiceProvider()
				.GetRequiredService<ILogger<RootCliCommand>>();
			ReflectionHelper.Logger = logger;
			return src;
		}
	}

}
