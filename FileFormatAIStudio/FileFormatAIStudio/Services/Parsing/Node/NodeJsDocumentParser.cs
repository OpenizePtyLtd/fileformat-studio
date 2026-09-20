using System;
using System.Collections.Generic;
using FileFormatAIStudio.Services.Benchmarking;

namespace FileFormatAIStudio.Services.Parsing.Node
{
    /// <summary>
    /// Multi-format document parser utilizing the Node.js officeparser engine.
    /// Supports .docx, .pptx, .xlsx, .odt, .odp, .ods, and .pdf via subprocess IPC.
    /// </summary>
    public class NodeJsDocumentParser : NodeJsDocumentParserBase
    {
        public const string ParserEngineId = "officeparser";

        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".docx", ".pptx", ".xlsx", ".odt", ".odp", ".ods", ".pdf"
        };

        public NodeJsDocumentParser(INodeJsHostService nodeHost) : base(nodeHost)
        {
        }

        public override DocumentCategory Category => DocumentCategory.Word;

        public override string EngineId => ParserEngineId;

        public override string DisplayName => "officeparser Universal (Node.js)";

        public override int Priority => 35;

        public override IReadOnlySet<string> SupportedExtensions => Extensions;

        protected override string NodeEngineId => ParserEngineId;
    }
}

