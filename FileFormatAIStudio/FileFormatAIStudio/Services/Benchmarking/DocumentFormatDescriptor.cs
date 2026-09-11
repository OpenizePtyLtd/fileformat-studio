namespace FileFormatAIStudio.Services.Benchmarking
{
    /// <summary>
    /// Describes a specific document file format/extension and its metadata.
    /// </summary>
    /// <param name="Extension">Normalized file extension with leading dot in lower-case (e.g. ".docx").</param>
    /// <param name="DisplayName">User-friendly format name (e.g. "Microsoft Word Document").</param>
    /// <param name="Category">Associated high-level document category.</param>
    /// <param name="MimeType">Primary MIME type associated with this format.</param>
    /// <param name="TypicalSourceApp">Typical desktop/suite application (e.g. "Microsoft Word", "Microsoft Excel").</param>
    /// <param name="Description">Brief description of the format and standards it represents.</param>
    public record DocumentFormatDescriptor(
        string Extension,
        string DisplayName,
        DocumentCategory Category,
        string MimeType,
        string TypicalSourceApp,
        string Description
    );
}

