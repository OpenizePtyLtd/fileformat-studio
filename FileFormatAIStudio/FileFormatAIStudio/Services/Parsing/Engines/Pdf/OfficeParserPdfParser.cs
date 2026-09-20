using System;
using System.Collections.Generic;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Parsing.Node;

namespace FileFormatAIStudio.Services.Parsing.Engines.Pdf
{
    /// <summary>
    /// PDF document parser utilizing the Node.js officeparser engine.
    /// Supports .pdf documents via subprocess IPC.
    /// </summary>
    public class OfficeParserPdfParser : NodeJsDocumentParserBase
    {
        public const string ParserEngineId = "officeparser-pdf";

        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf"
        };

        public OfficeParserPdfParser(INodeJsHostService nodeHost) : base(nodeHost)
        {
        }

        public override DocumentCategory Category => DocumentCategory.Pdf;

        public override string EngineId => ParserEngineId;

        public override string DisplayName => "officeparser (Node.js)";

        public override int Priority => 30;

        public override IReadOnlySet<string> SupportedExtensions => Extensions;

        protected override string NodeEngineId => "officeparser";
    }
}

