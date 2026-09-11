using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileFormatAIStudio.Services.Benchmarking
{
    /// <summary>
    /// Default implementation of IDocumentCategoryRegistry providing complete format taxonomy and resolution.
    /// </summary>
    public class DocumentCategoryRegistry : IDocumentCategoryRegistry
    {
        private static readonly IReadOnlyList<DocumentCategory> AllCategoriesList = new[]
        {
            DocumentCategory.Word,
            DocumentCategory.Excel,
            DocumentCategory.PowerPoint,
            DocumentCategory.Pdf,
            DocumentCategory.PlainText
        };

        private readonly Dictionary<string, DocumentFormatDescriptor> _formatsByExtension;
        private readonly List<DocumentFormatDescriptor> _allFormats;

        public DocumentCategoryRegistry()
        {
            _allFormats = new List<DocumentFormatDescriptor>
            {
                // Word Processing
                new(".docx", "Word Document (OpenXML)", DocumentCategory.Word, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "Microsoft Word", "Modern XML-based zipped document format introduced in Word 2007."),
                new(".doc", "Word 97-2003 Document", DocumentCategory.Word, "application/msword", "Microsoft Word", "Legacy OLE2 compound binary document format used prior to Office 2007."),
                new(".dotx", "Word Template (OpenXML)", DocumentCategory.Word, "application/vnd.openxmlformats-officedocument.wordprocessingml.template", "Microsoft Word", "Template format for generating new OpenXML Word documents."),
                new(".dot", "Word 97-2003 Template", DocumentCategory.Word, "application/msword", "Microsoft Word", "Legacy binary document template format."),
                new(".docm", "Word Macro-Enabled Document", DocumentCategory.Word, "application/vnd.ms-word.document.macroEnabled.12", "Microsoft Word", "OpenXML Word document containing embedded VBA macros."),
                new(".rtf", "Rich Text Format", DocumentCategory.Word, "application/rtf", "Microsoft Word / WordPad", "Cross-platform formatted document standard developed by Microsoft."),
                new(".odt", "OpenDocument Text", DocumentCategory.Word, "application/vnd.oasis.opendocument.text", "LibreOffice Writer / OpenOffice", "OASIS OpenDocument standard XML format for text documents."),

                // Spreadsheets
                new(".xlsx", "Excel Workbook (OpenXML)", DocumentCategory.Excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Microsoft Excel", "Modern XML-based zipped spreadsheet format introduced in Excel 2007."),
                new(".xls", "Excel 97-2003 Workbook", DocumentCategory.Excel, "application/vnd.ms-excel", "Microsoft Excel", "Legacy BIFF8 binary workbook format used prior to Office 2007."),
                new(".xlsm", "Excel Macro-Enabled Workbook", DocumentCategory.Excel, "application/vnd.ms-excel.sheet.macroEnabled.12", "Microsoft Excel", "OpenXML spreadsheet containing embedded VBA macros."),
                new(".xlsb", "Excel Binary Workbook", DocumentCategory.Excel, "application/vnd.ms-excel.sheet.binary.macroEnabled.12", "Microsoft Excel", "High-performance binary workbook format (BIFF12) for large datasets."),
                new(".xltx", "Excel Template (OpenXML)", DocumentCategory.Excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.template", "Microsoft Excel", "Template format for creating new OpenXML workbooks."),
                new(".xlt", "Excel 97-2003 Template", DocumentCategory.Excel, "application/vnd.ms-excel", "Microsoft Excel", "Legacy binary spreadsheet template."),
                new(".ods", "OpenDocument Spreadsheet", DocumentCategory.Excel, "application/vnd.oasis.opendocument.spreadsheet", "LibreOffice Calc / OpenOffice", "OASIS OpenDocument standard XML format for spreadsheets."),
                new(".csv", "Comma-Separated Values", DocumentCategory.Excel, "text/csv", "Microsoft Excel / Text Editor", "Delimited text file storing tabular data row by row."),
                new(".tsv", "Tab-Separated Values", DocumentCategory.Excel, "text/tab-separated-values", "Microsoft Excel / Text Editor", "Tab-delimited text file storing tabular data."),

                // Presentations
                new(".pptx", "PowerPoint Presentation (OpenXML)", DocumentCategory.PowerPoint, "application/vnd.openxmlformats-officedocument.presentationml.presentation", "Microsoft PowerPoint", "Modern XML-based presentation format introduced in PowerPoint 2007."),
                new(".ppt", "PowerPoint 97-2003 Presentation", DocumentCategory.PowerPoint, "application/vnd.ms-powerpoint", "Microsoft PowerPoint", "Legacy binary slide presentation format."),
                new(".ppsx", "PowerPoint Slide Show", DocumentCategory.PowerPoint, "application/vnd.openxmlformats-officedocument.presentationml.slideshow", "Microsoft PowerPoint", "OpenXML presentation that opens directly into full-screen slide show mode."),
                new(".pps", "PowerPoint 97-2003 Slide Show", DocumentCategory.PowerPoint, "application/vnd.ms-powerpoint", "Microsoft PowerPoint", "Legacy binary full-screen slide show."),
                new(".potx", "PowerPoint Template (OpenXML)", DocumentCategory.PowerPoint, "application/vnd.openxmlformats-officedocument.presentationml.template", "Microsoft PowerPoint", "OpenXML template format for PowerPoint decks."),
                new(".pot", "PowerPoint 97-2003 Template", DocumentCategory.PowerPoint, "application/vnd.ms-powerpoint", "Microsoft PowerPoint", "Legacy binary PowerPoint template."),
                new(".odp", "OpenDocument Presentation", DocumentCategory.PowerPoint, "application/vnd.oasis.opendocument.presentation", "LibreOffice Impress / OpenOffice", "OASIS OpenDocument standard format for slide presentations."),

                // PDF
                new(".pdf", "Portable Document Format", DocumentCategory.Pdf, "application/pdf", "Adobe Acrobat / Web Browser", "Standardized ISO 32000 format representing fixed-layout documents."),

                // Plain & Structured Text
                new(".txt", "Plain Text Document", DocumentCategory.PlainText, "text/plain", "Notepad / Text Editor", "Unformatted standard text stream encoded in UTF-8 or ASCII."),
                new(".md", "Markdown Document", DocumentCategory.PlainText, "text/markdown", "Markdown Editor", "Lightweight markup language with plain text formatting syntax."),
                new(".markdown", "Markdown Document", DocumentCategory.PlainText, "text/markdown", "Markdown Editor", "Extended extension for Markdown files."),
                new(".json", "JSON Document", DocumentCategory.PlainText, "application/json", "Code Editor", "JavaScript Object Notation structured data file."),
                new(".xml", "XML Document", DocumentCategory.PlainText, "application/xml", "Code Editor", "Extensible Markup Language document."),
                new(".html", "HTML Document", DocumentCategory.PlainText, "text/html", "Web Browser", "HyperText Markup Language file."),
                new(".htm", "HTML Document", DocumentCategory.PlainText, "text/html", "Web Browser", "HyperText Markup Language file.")
            };

            _formatsByExtension = new Dictionary<string, DocumentFormatDescriptor>(StringComparer.OrdinalIgnoreCase);
            foreach (var descriptor in _allFormats)
            {
                _formatsByExtension[descriptor.Extension] = descriptor;
            }
        }

        public IReadOnlyList<DocumentCategory> GetAllCategories() => AllCategoriesList;

        public string GetCategoryDisplayName(DocumentCategory category) => category switch
        {
            DocumentCategory.Word => "Word Document",
            DocumentCategory.Excel => "Excel Spreadsheet",
            DocumentCategory.PowerPoint => "PowerPoint Presentation",
            DocumentCategory.Pdf => "PDF Document",
            DocumentCategory.PlainText => "Plain / Structured Text",
            _ => category.ToString()
        };

        public IReadOnlyList<DocumentFormatDescriptor> GetAllFormats() => _allFormats.AsReadOnly();

        public IReadOnlyList<DocumentFormatDescriptor> GetFormatsByCategory(DocumentCategory category) =>
            _allFormats.Where(f => f.Category == category).ToList().AsReadOnly();

        public DocumentFormatDescriptor? GetFormatDescriptor(string filePathOrExtension)
        {
            var ext = NormalizeExtension(filePathOrExtension);
            if (string.IsNullOrEmpty(ext))
                return null;

            return _formatsByExtension.TryGetValue(ext, out var descriptor) ? descriptor : null;
        }

        public DocumentCategory ResolveCategory(string filePathOrExtension)
        {
            var descriptor = GetFormatDescriptor(filePathOrExtension);
            if (descriptor != null)
                return descriptor.Category;

            var ext = NormalizeExtension(filePathOrExtension);
            return ext switch
            {
                ".docx" or ".doc" or ".dot" or ".dotx" or ".docm" or ".rtf" or ".odt" or ".wps" => DocumentCategory.Word,
                ".xlsx" or ".xls" or ".xlsm" or ".xlsb" or ".xltx" or ".xlt" or ".ods" or ".csv" or ".tsv" => DocumentCategory.Excel,
                ".pptx" or ".ppt" or ".ppsx" or ".pps" or ".potx" or ".pot" or ".odp" => DocumentCategory.PowerPoint,
                ".pdf" => DocumentCategory.Pdf,
                _ => DocumentCategory.PlainText
            };
        }

        public bool IsSupported(string filePathOrExtension)
        {
            var ext = NormalizeExtension(filePathOrExtension);
            return !string.IsNullOrEmpty(ext) && _formatsByExtension.ContainsKey(ext);
        }

        public IReadOnlySet<string> GetSupportedExtensions(DocumentCategory? category = null)
        {
            var query = category.HasValue
                ? _allFormats.Where(f => f.Category == category.Value)
                : _allFormats;

            return new HashSet<string>(query.Select(f => f.Extension), StringComparer.OrdinalIgnoreCase);
        }

        public static string NormalizeExtension(string filePathOrExtension)
        {
            if (string.IsNullOrWhiteSpace(filePathOrExtension))
                return string.Empty;

            var trimmed = filePathOrExtension.Trim();
            var ext = Path.GetExtension(trimmed);
            if (string.IsNullOrEmpty(ext))
            {
                ext = trimmed;
            }

            if (string.IsNullOrWhiteSpace(ext))
                return string.Empty;

            if (!ext.StartsWith('.'))
                ext = "." + ext;

            return ext.ToLowerInvariant();
        }
    }
}

