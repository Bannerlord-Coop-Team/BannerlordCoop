using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Common.Serialization
{
    /// <summary>
    /// Reads and writes a JSON compatible object to a given file path
    /// </summary>
    public interface IJsonFileIO
    {
        /// <summary>
        /// Reads and casts a JSON compatible object from the given path
        /// </summary>
        /// <typeparam name="T">Type to cast loaded object</typeparam>
        /// <param name="path">Path to read from</param>
        /// <returns>Deserialized object</returns>
        T ReadFromFile<T>(string filePath);
        /// <summary>
        /// Writes a given object as a JSON file to the given path
        /// NOTE: Path must include .json postfix
        /// </summary>
        /// <typeparam name="T">Type of object</typeparam>
        /// <param name="path">Path to write object to</param>
        /// <param name="obj">Object to write to path</param>
        void WriteToFile<T>(string filePath, T obj) where T : class;
    }

    /// <inheritdoc/>
    public class JsonFileIO : IJsonFileIO
    {
        /// <summary>
        /// File encoding format
        /// </summary>
        public Encoding Encoding { get; set; } = Encoding.UTF8;
        /// <summary>
        /// Creates the filePath directory if it does not exist
        /// </summary>
        public bool CreateDirectory { get; set; } = true;
        /// <summary>
        /// Options for json serialization
        /// </summary>
        public JsonSerializerOptions JsonOptions { get; set; } = new JsonSerializerOptions()
        {
            WriteIndented = true,
        };

        /// <inheritdoc/>
        public void WriteToFile<T>(string filePath, T obj) where T : class
        {
            if (filePath.EndsWith(".json") == false)
            {
                throw new FormatException("Path did not end with '.json' file name");
            }

            // Create directory if it does not exist
            if(CreateDirectory)
            {
                string path = Path.GetDirectoryName(filePath);

                if (Directory.Exists(path) == false)
                    Directory.CreateDirectory(path);
            }

            string jsonText = JsonSerializer.Serialize(obj, JsonOptions);
            File.WriteAllText(filePath, jsonText, Encoding);
        }

        /// <inheritdoc/>
        public T ReadFromFile<T>(string filePath)
        {
            string jsonText = File.ReadAllText(filePath, Encoding);
            return JsonSerializer.Deserialize<T>(jsonText);
        }
    }
}
