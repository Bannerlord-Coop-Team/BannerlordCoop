using Common.Serialization;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Common.Tests.Serialization
{
    internal record TestClass(string SomeValue)
    {
        [JsonInclude]
        public string SomeValue { get; private set; } = SomeValue;
    }

    public class JsonFileIOTests
    {
        private const string OUTPUT_PATH = "./TestFile.json";

        [Fact]
        public void Write()
        {
            IJsonFileIO fileIO = new JsonFileIO();

            fileIO.WriteToFile(OUTPUT_PATH, new TestClass("SomeValue"));
            fileIO.WriteToFile(OUTPUT_PATH, new TestClass("SomeValue2"));
        }

        [Fact]
        public void WriteInvalidPath()
        {
            IJsonFileIO fileIO = new JsonFileIO();

            var testClass = new TestClass("SomeValue");

            // Test that a directory cannot be written to
            Assert.Throws<FormatException>(() => fileIO.WriteToFile(".", testClass));
        }

        [Fact]
        public void WriteInvalidFileName()
        {
            IJsonFileIO fileIO = new JsonFileIO();

            var testClass = new TestClass("SomeValue");

            // Verify .json is required in file path
            Assert.Throws<FormatException>(() => fileIO.WriteToFile("./TestFile", testClass));
        }

        [Fact]
        public void WriteInvalidObjectCase()
        {
            IJsonFileIO fileIO = new JsonFileIO();

            fileIO.WriteToFile(OUTPUT_PATH, new TestClass("SomeValue"));

            // Verify invalid cast throws exception
            Assert.Throws<System.Text.Json.JsonException>(() => fileIO.ReadFromFile<int>(OUTPUT_PATH));
        }

        [Fact]
        public void WriteThenRead()
        {
            IJsonFileIO fileIO = new JsonFileIO();

            var testClass = new TestClass("SomeValue");

            fileIO.WriteToFile(OUTPUT_PATH, testClass);

            TestClass resolvedClass = fileIO.ReadFromFile<TestClass>(OUTPUT_PATH);
            
            Assert.NotNull(resolvedClass);
            Assert.Equal(testClass, resolvedClass);
        }
    }
}
