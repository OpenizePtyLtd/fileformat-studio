using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Parsing;
using FileFormatAIStudio.Services.Parsing.Node;
using FluentAssertions;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class NodeJsHostServiceTests
    {
        private readonly NodeJsRuntimeService _runtimeService;
        private readonly NodeJsHostService _hostService;

        public NodeJsHostServiceTests()
        {
            _runtimeService = new NodeJsRuntimeService();
            _hostService = new NodeJsHostService(_runtimeService);
        }

        [Fact]
        public void RuntimeService_DetectsNodeExecutable_WhenInstalled()
        {
            _runtimeService.IsAvailable.Should().BeTrue("Node.js should be available on the machine or in PATH");
            _runtimeService.NodeExecutablePath.Should().NotBeNullOrWhiteSpace();
            File.Exists(_runtimeService.NodeExecutablePath).Should().BeTrue();
        }

        [Fact]
        public void RuntimeService_LocatesNodeHostScript()
        {
            _runtimeService.NodeHostScriptPath.Should().NotBeNullOrWhiteSpace();
            File.Exists(_runtimeService.NodeHostScriptPath).Should().BeTrue();
            Path.GetFileName(_runtimeService.NodeHostScriptPath).Should().Be("index.js");
        }

        [Fact]
        public async Task RuntimeService_GetRuntimeInfoAsync_ProbesNodeAndEngines()
        {
            var info = await _runtimeService.GetRuntimeInfoAsync(forceRefresh: true);

            info.Should().NotBeNull();
            info.IsAvailable.Should().BeTrue();
            info.NodeVersion.Should().StartWith("v");
            info.InstalledEngines.Should().NotBeEmpty();
            info.InstalledEngines.Should().Contain(e => e.Id == "officeparser");
        }

        [Fact]
        public void RuntimeService_CustomPath_OverridesResolution()
        {
            var fakePath = @"C:\NonExistent\node.exe";
            _runtimeService.SetCustomNodeExecutablePath(fakePath);

            _runtimeService.NodeExecutablePath.Should().Be(fakePath);

            // Reset back
            _runtimeService.SetCustomNodeExecutablePath(null);
            _runtimeService.NodeExecutablePath.Should().NotBe(fakePath);
        }

        [Fact]
        public async Task HostService_ExecuteProbe_ReturnsSuccessWithEngines()
        {
            var req = new NodeJsRequest
            {
                Command = "probe"
            };

            var res = await _hostService.ExecuteAsync(req);

            res.Should().NotBeNull();
            res.Success.Should().BeTrue();
            res.Error.Should().BeNull();
            res.Metadata.Should().NotBeNull();
            res.Metadata!.ContainsKey("engines").Should().BeTrue();
        }

        [Fact]
        public async Task HostService_ExecuteWordExtraction_WithOfficeParser_Succeeds()
        {
            var tempDocx = Path.Combine(Path.GetTempPath(), $"test_node_docx_{Guid.NewGuid():N}.docx");
            try
            {
                var doc = new Aspose.Words.Document();
                var builder = new Aspose.Words.DocumentBuilder(doc);
                builder.Writeln("Hello from Node.js officeparser integration test!");
                builder.Writeln("Architectural spike validation paragraph.");
                doc.Save(tempDocx);

                var response = await _hostService.ExecuteAsync("officeparser", "extractText", tempDocx);

                response.Should().NotBeNull();
                response.Success.Should().BeTrue(response.Error);
                response.Text.Should().NotBeNullOrWhiteSpace();
                response.Text.Should().Contain("Hello from Node.js officeparser integration test!");
                response.CharacterCount.Should().BeGreaterThan(0);
                response.ExecutionMs.Should().BeGreaterThan(0);
            }
            finally
            {
                if (File.Exists(tempDocx)) File.Delete(tempDocx);
            }
        }

        [Fact]
        public async Task HostService_ExecuteExcelExtraction_WithOfficeParser_Succeeds()
        {
            var tempXlsx = Path.Combine(Path.GetTempPath(), $"test_node_xlsx_{Guid.NewGuid():N}.xlsx");
            try
            {
                var workbook = new Aspose.Cells.Workbook();
                var sheet = workbook.Worksheets[0];
                sheet.Cells["A1"].PutValue("Product");
                sheet.Cells["B1"].PutValue("Price");
                sheet.Cells["A2"].PutValue("OfficeParser");
                sheet.Cells["B2"].PutValue(99);
                workbook.Save(tempXlsx);

                var response = await _hostService.ExecuteAsync("officeparser", "extractText", tempXlsx);

                response.Should().NotBeNull();
                response.Success.Should().BeTrue(response.Error);
                response.Text.Should().NotBeNullOrWhiteSpace();
                response.Text.Should().Contain("OfficeParser");
            }
            finally
            {
                if (File.Exists(tempXlsx)) File.Delete(tempXlsx);
            }
        }

        [Fact]
        public async Task HostService_ExecuteMissingFile_ReturnsErrorResponse()
        {
            var nonExistentPath = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}.docx");

            var response = await _hostService.ExecuteAsync("officeparser", "extractText", nonExistentPath);

            response.Should().NotBeNull();
            response.Success.Should().BeFalse();
            response.Error.Should().NotBeNullOrWhiteSpace();
            response.Error.Should().Contain("File not found");
        }

        [Fact]
        public async Task HostService_CancelledRequest_ReturnsCancelledResponse()
        {
            var tempDocx = Path.Combine(Path.GetTempPath(), $"test_cancel_{Guid.NewGuid():N}.docx");
            try
            {
                var doc = new Aspose.Words.Document();
                var builder = new Aspose.Words.DocumentBuilder(doc);
                builder.Writeln("Cancellation test document.");
                doc.Save(tempDocx);

                using var cts = new CancellationTokenSource();
                cts.Cancel(); // Pre-cancel

                var response = await _hostService.ExecuteAsync("officeparser", "extractText", tempDocx, cts.Token);

                response.Should().NotBeNull();
                response.Success.Should().BeFalse();
                response.Error.Should().Contain("cancelled");
            }
            finally
            {
                if (File.Exists(tempDocx)) File.Delete(tempDocx);
            }
        }

        private class SampleNodeParser : NodeJsDocumentParserBase
        {
            public SampleNodeParser(INodeJsHostService hostService) : base(hostService) { }

            public override DocumentCategory Category => DocumentCategory.Word;
            public override string EngineId => "officeparser";
            public override string DisplayName => "officeparser (Node.js)";
            public override int Priority => 60;
            public override IReadOnlySet<string> SupportedExtensions => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".docx" };
        }

        [Fact]
        public async Task NodeJsDocumentParserBase_ExtractTextAsync_DelegatesProperly()
        {
            var parser = new SampleNodeParser(_hostService);
            parser.Category.Should().Be(DocumentCategory.Word);
            parser.EngineId.Should().Be("officeparser");
            parser.IsAvailable.Should().BeTrue();

            var tempDocx = Path.Combine(Path.GetTempPath(), $"test_base_adapter_{Guid.NewGuid():N}.docx");
            try
            {
                var doc = new Aspose.Words.Document();
                var builder = new Aspose.Words.DocumentBuilder(doc);
                builder.Writeln("Adapter Delegation Content");
                doc.Save(tempDocx);

                var text = await parser.ExtractTextAsync(tempDocx);
                text.Should().Contain("Adapter Delegation Content");
            }
            finally
            {
                if (File.Exists(tempDocx)) File.Delete(tempDocx);
            }
        }
    }
}

