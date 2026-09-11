namespace FileFormatAIStudio.Services.Benchmarking
{
    /// <summary>
    /// High-level application/document category used for grouping benchmark tests.
    /// </summary>
    public enum DocumentCategory
    {
        /// <summary>
        /// Word processing documents (e.g. .docx, .doc, .dot, .dotx, .rtf, .odt).
        /// </summary>
        Word,

        /// <summary>
        /// Spreadsheets and tabular workbooks (e.g. .xlsx, .xls, .xlsm, .xlsb, .ods, .csv, .tsv).
        /// </summary>
        Excel,

        /// <summary>
        /// Presentations and slide decks (e.g. .pptx, .ppt, .pps, .ppsx, .odp).
        /// </summary>
        PowerPoint,

        /// <summary>
        /// Portable Document Format (e.g. .pdf - searchable, form fields, scanned).
        /// </summary>
        Pdf,

        /// <summary>
        /// Plain and structured text files (e.g. .txt, .md, .markdown, .json, .xml, .html).
        /// </summary>
        PlainText
    }
}

