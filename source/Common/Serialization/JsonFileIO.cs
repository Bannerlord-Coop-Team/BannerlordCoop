using Common.Logging;
using Serilog;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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
        private static readonly ILogger Logger = LogManager.GetLogger<JsonFileIO>();

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

            var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromSeconds(1));
            Task.Factory.StartNew(async () => { await AsyncWriteToFile(filePath, jsonText, tokenSource.Token); }, tokenSource.Token);
        }

        private async Task AsyncWriteToFile(string filePath, string jsonText, CancellationToken cancellationToken)
        {
            while (cancellationToken.IsCancellationRequested == false)
            {
                try
                {
                    File.WriteAllText(filePath, jsonText, Encoding);
                    return;
                }
                catch (IOException)
                {
                    // IO did not work, try again after a delay
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
            }

            Logger.Error("Unable to write to {fileName}", filePath);
        }

        /// <inheritdoc/>
        public T ReadFromFile<T>(string filePath)
        {
            string jsonText = File.ReadAllText(filePath, Encoding);
            return JsonSerializer.Deserialize<T>(jsonText);
        }
    }
}
