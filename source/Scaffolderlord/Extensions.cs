using System;
using System.Linq;
using System.Reflection;

namespace Scaffolderlord
{
    public static class Extensions
    {
        #region Path
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
        #endregion

        #region Reflection
        public static IEnumerable<PropertyInfo> GetPropertiesWithSetters(Type type)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic);

            return properties
                .Where(prop => prop.CanWrite);
        }

		public static T CreateInstance<T>(params object[] paramArray)
		{
			return (T)Activator.CreateInstance(typeof(T), args: paramArray);
		}
		#endregion
	}

}
