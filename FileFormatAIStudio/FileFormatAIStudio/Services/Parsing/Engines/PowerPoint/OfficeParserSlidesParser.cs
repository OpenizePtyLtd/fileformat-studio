using System;
using System.Collections.Generic;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Parsing.Node;

namespace FileFormatAIStudio.Services.Parsing.Engines.PowerPoint
{
    /// <summary>
    /// Presentation document parser utilizing the Node.js officeparser engine.
    /// Supports .pptx and .odp presentations via subprocess IPC.
    /// </summary>
    public class OfficeParserSlidesParser : NodeJsDocumentParserBase
    {
        public const string ParserEngineId = "officeparser-slides";

        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pptx", ".odp"
        };

        public OfficeParserSlidesParser(INodeJsHostService nodeHost) : base(nodeHost)
        {
        }

        public override DocumentCategory Category => DocumentCategory.PowerPoint;

        public override string EngineId => ParserEngineId;

        public override string DisplayName => "officeparser (Node.js)";

        public override int Priority => 40;

        public override IReadOnlySet<string> SupportedExtensions => Extensions;

        protected override string NodeEngineId => "officeparser";
    }
}

