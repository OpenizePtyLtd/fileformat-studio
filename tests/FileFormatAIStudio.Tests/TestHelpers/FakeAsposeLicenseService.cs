using System;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Parsing;

namespace FileFormatAIStudio.Tests.TestHelpers
{
    public class FakeAsposeLicenseService : IAsposeLicenseService
    {
        public bool IsLicensed { get; set; }
        public bool IsWordsLicensed { get; set; }
        public bool IsCellsLicensed { get; set; }
        public bool IsSlidesLicensed { get; set; }
        public bool IsPdfLicensed { get; set; }
        public string? ActiveLicensePath { get; set; }

        public FakeAsposeLicenseService(bool allLicensed = false)
        {
            IsLicensed = allLicensed;
            IsWordsLicensed = allLicensed;
            IsCellsLicensed = allLicensed;
            IsSlidesLicensed = allLicensed;
            IsPdfLicensed = allLicensed;
        }

        public bool IsCategoryLicensed(DocumentCategory category) => category switch
        {
            DocumentCategory.Pdf => IsPdfLicensed,
            DocumentCategory.Word => IsWordsLicensed,
            DocumentCategory.Excel => IsCellsLicensed,
            DocumentCategory.PowerPoint => IsSlidesLicensed,
            DocumentCategory.PlainText => true,
            _ => IsLicensed
        };

        public bool IsEngineLicensed(string engineId)
        {
            if (string.IsNullOrWhiteSpace(engineId)) return true;
            if (!engineId.Contains("aspose", StringComparison.OrdinalIgnoreCase)) return true;
            if (engineId.Contains("pdf", StringComparison.OrdinalIgnoreCase)) return IsPdfLicensed;
            if (engineId.Contains("word", StringComparison.OrdinalIgnoreCase)) return IsWordsLicensed;
            if (engineId.Contains("cell", StringComparison.OrdinalIgnoreCase) || engineId.Contains("excel", StringComparison.OrdinalIgnoreCase)) return IsCellsLicensed;
            if (engineId.Contains("slide", StringComparison.OrdinalIgnoreCase) || engineId.Contains("powerpoint", StringComparison.OrdinalIgnoreCase)) return IsSlidesLicensed;
            return IsLicensed;
        }

        public string GetEvaluationNotice(DocumentCategory category) => "Evaluation Notice";
        public string GetEvaluationNotice(string engineId) => "Evaluation Notice";
        public void InitializeLicenses(string? explicitLicensePath = null) { }

        public bool InstallLicense(string sourceFilePath)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath)) return false;
            IsLicensed = true;
            IsWordsLicensed = true;
            IsCellsLicensed = true;
            IsSlidesLicensed = true;
            IsPdfLicensed = true;
            ActiveLicensePath = sourceFilePath;
            return true;
        }

        public bool RemoveLicense()
        {
            IsLicensed = false;
            IsWordsLicensed = false;
            IsCellsLicensed = false;
            IsSlidesLicensed = false;
            IsPdfLicensed = false;
            ActiveLicensePath = null;
            return true;
        }
    }
}

