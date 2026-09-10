using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FileFormatAIStudio.ViewModels
{
    /// <summary>
    /// ViewModel representing an individual file chosen for indexing in the Knowledgebase creation wizard.
    /// </summary>
    public partial class SelectedFileItemViewModel : ObservableObject
    {
        public string FilePath { get; }
        public string FileName { get; }
        public string FileExtension { get; }
        public long FileSizeBytes { get; }
        public string FormattedFileSize { get; }
        public string FileIconGlyph { get; }

        public event Action<SelectedFileItemViewModel>? RemoveRequested;

        public SelectedFileItemViewModel(string filePath, long fileSizeBytes)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            FileName = Path.GetFileName(filePath);
            FileExtension = Path.GetExtension(filePath).ToLowerInvariant();
            FileSizeBytes = fileSizeBytes;
            FormattedFileSize = FormatBytes(fileSizeBytes);
            FileIconGlyph = ResolveGlyphForExtension(FileExtension);
        }

        [RelayCommand]
        private void Remove()
        {
            RemoveRequested?.Invoke(this);
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }

        public static string ResolveGlyphForExtension(string ext)
        {
            return ext switch
            {
                ".docx" or ".doc" => "\uE8A5", // Document
                ".xlsx" or ".xls" => "\uE9F9", // Spreadsheet / Table
                ".pptx" or ".ppt" => "\uE8B7", // Presentation / Slides
                ".pdf" => "\uEA90",            // PDF document
                ".txt" or ".csv" or ".md" => "\uE8C8", // Plain text / Code / CSV
                _ => "\uE8A5"                  // Default Document
            };
        }
    }
}

