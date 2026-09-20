using System;
using System.Collections.Generic;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Parsing.Node;

namespace FileFormatAIStudio.Services.Parsing.Engines.Excel
{
    /// <summary>
    /// Spreadsheet document parser utilizing the Node.js officeparser engine.
    /// Supports .xlsx and .ods spreadsheets via subprocess IPC.
    /// </summary>
    public class OfficeParserExcelParser : NodeJsDocumentParserBase
    {
        public const string ParserEngineId = "officeparser-cells";

        private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".xlsx", ".ods"
        };

        public OfficeParserExcelParser(INodeJsHostService nodeHost) : base(nodeHost)
        {
        }

        public override DocumentCategory Category => DocumentCategory.Excel;

        public override string EngineId => ParserEngineId;

        public override string DisplayName => "officeparser (Node.js)";

        public override int Priority => 30;

        public override IReadOnlySet<string> SupportedExtensions => Extensions;

        protected override string NodeEngineId => "officeparser";
    }
}

