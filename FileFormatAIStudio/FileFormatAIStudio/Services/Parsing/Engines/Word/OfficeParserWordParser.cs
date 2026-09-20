using System;
using System.Collections.Generic;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Parsing.Node;

namespace FileFormatAIStudio.Services.Parsing.Engines.Word
{
    /// <summary>
    /// Word processing document parser utilizing the Node.js officeparser engine.
    /// Supports .docx and .odt documents via subprocess IPC.
    /// </summary>
    public class OfficeParserWordParser : NodeJsDocumentParserBase
    {
        public const string ParserEngineId = "officeparser-words";

        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".docx", ".odt"
        };

        public OfficeParserWordParser(INodeJsHostService nodeHost) : base(nodeHost)
        {
        }

        public override DocumentCategory Category => DocumentCategory.Word;

        public override string EngineId => ParserEngineId;

        public override string DisplayName => "officeparser (Node.js)";

        public override int Priority => 30;

        public override IReadOnlySet<string> SupportedExtensions => Extensions;

        protected override string NodeEngineId => "officeparser";
    }
}

