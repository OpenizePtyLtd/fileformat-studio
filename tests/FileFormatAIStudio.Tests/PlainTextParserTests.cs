using System;
using System.IO;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Parsing;
using FluentAssertions;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class PlainTextParserTests
    {
        private readonly PlainTextParser _parser = new();

        [Fact]
        public void Properties_HaveExpectedDefaults()
        {
            _parser.EngineId.Should().Be("plaintext");
            _parser.DisplayName.Should().Contain("Plain Text");
            _parser.Priority.Should().Be(10);
            _parser.IsAvailable.Should().BeTrue();
            _parser.SupportedExtensions.Should().Contain(new[] { ".txt", ".md", ".json", ".csv", ".xml", ".log", ".yaml" });
        }

        [Fact]
        public async Task ExtractTextAsync_ReturnsFileContent()
        {
            var tempFile = Path.GetTempFileName();
            var expectedContent = "Line 1: Hello World\r\nLine 2: Knowledgebase text extraction test.";
            await File.WriteAllTextAsync(tempFile, expectedContent);

            try
            {
                var result = await _parser.ExtractTextAsync(tempFile);
                result.Should().Be(expectedContent);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task ExtractTextAsync_ThrowsFileNotFoundException_WhenFileDoesNotExist()
        {
            var act = async () => await _parser.ExtractTextAsync(@"C:\nonexistent_text_xyz_987.txt");
            await act.Should().ThrowAsync<FileNotFoundException>();
        }

        [Fact]
        public async Task ExtractTextAsync_ThrowsArgumentException_WhenPathIsEmpty()
        {
            var act = async () => await _parser.ExtractTextAsync("");
            await act.Should().ThrowAsync<ArgumentException>();
        }
    }
}

