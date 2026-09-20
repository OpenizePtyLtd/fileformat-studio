using System;
using System.IO;
using FileFormatAIStudio.Services.Benchmarking;

namespace FileFormatAIStudio.Services.Parsing
{
    /// <summary>
    /// Contract for managing and applying Aspose licenses across all integrated Aspose libraries.
    /// </summary>
    public interface IAsposeLicenseService
    {
        /// <summary>
        /// Gets whether an Aspose license has been successfully applied to any product.
        /// </summary>
        bool IsLicensed { get; }

        /// <summary>
        /// Gets whether Aspose.Words is actively licensed.
        /// </summary>
        bool IsWordsLicensed { get; }

        /// <summary>
        /// Gets whether Aspose.Cells is actively licensed.
        /// </summary>
        bool IsCellsLicensed { get; }

        /// <summary>
        /// Gets whether Aspose.Slides is actively licensed.
        /// </summary>
        bool IsSlidesLicensed { get; }

        /// <summary>
        /// Gets whether Aspose.Pdf is actively licensed.
        /// </summary>
        bool IsPdfLicensed { get; }

        /// <summary>
        /// Gets the path to the currently active license file, if one was found and applied.
        /// </summary>
        string? ActiveLicensePath { get; }

        /// <summary>
        /// Determines whether the specified document category has an active Aspose license.
        /// Non-Aspose or PlainText categories return true (no license required).
        /// </summary>
        bool IsCategoryLicensed(DocumentCategory category);

        /// <summary>
        /// Determines whether the specified engine ID has an active Aspose license.
        /// Non-Aspose engines return true (no license required).
        /// </summary>
        bool IsEngineLicensed(string engineId);

        /// <summary>
        /// Gets an evaluation warning or limitation notice for the given category.
        /// </summary>
        string GetEvaluationNotice(DocumentCategory category);

        /// <summary>
        /// Gets an evaluation warning or limitation notice for the given engine ID.
        /// </summary>
        string GetEvaluationNotice(string engineId);

        /// <summary>
        /// Attempts to initialize Aspose licenses from configured or discovered paths.
        /// If no valid license is available, operates gracefully in evaluation mode.
        /// </summary>
        void InitializeLicenses(string? explicitLicensePath = null);
    }

    /// <summary>
    /// Service responsible for locating and applying Aspose product licenses with evaluation fallback.
    /// </summary>
    public class AsposeLicenseService : IAsposeLicenseService
    {
        private static readonly object SyncLock = new();
        private bool _isLicensed;
        private bool _isWordsLicensed;
        private bool _isCellsLicensed;
        private bool _isSlidesLicensed;
        private bool _isPdfLicensed;
        private string? _activeLicensePath;

        public bool IsLicensed => _isLicensed;
        public bool IsWordsLicensed => _isWordsLicensed;
        public bool IsCellsLicensed => _isCellsLicensed;
        public bool IsSlidesLicensed => _isSlidesLicensed;
        public bool IsPdfLicensed => _isPdfLicensed;
        public string? ActiveLicensePath => _activeLicensePath;

        public AsposeLicenseService()
        {
            InitializeLicenses();
        }

        public bool IsCategoryLicensed(DocumentCategory category) => category switch
        {
            DocumentCategory.Pdf => _isPdfLicensed,
            DocumentCategory.Word => _isWordsLicensed,
            DocumentCategory.Excel => _isCellsLicensed,
            DocumentCategory.PowerPoint => _isSlidesLicensed,
            DocumentCategory.PlainText => true,
            _ => _isLicensed
        };

        public bool IsEngineLicensed(string engineId)
        {
            if (string.IsNullOrWhiteSpace(engineId)) return true;
            if (!engineId.Contains("aspose", StringComparison.OrdinalIgnoreCase)) return true;

            if (engineId.Contains("pdf", StringComparison.OrdinalIgnoreCase)) return _isPdfLicensed;
            if (engineId.Contains("word", StringComparison.OrdinalIgnoreCase)) return _isWordsLicensed;
            if (engineId.Contains("cell", StringComparison.OrdinalIgnoreCase) || engineId.Contains("excel", StringComparison.OrdinalIgnoreCase)) return _isCellsLicensed;
            if (engineId.Contains("slide", StringComparison.OrdinalIgnoreCase) || engineId.Contains("powerpoint", StringComparison.OrdinalIgnoreCase)) return _isSlidesLicensed;

            return _isLicensed;
        }

        public string GetEvaluationNotice(DocumentCategory category) => category switch
        {
            DocumentCategory.Pdf => "Evaluation Mode: Aspose.PDF processes only the first 4 pages of any PDF document. Extraction volume is capped.",
            DocumentCategory.Word => "Evaluation Mode: Aspose.Words truncates text and injects evaluation copyright watermarks.",
            DocumentCategory.Excel => "Evaluation Mode: Aspose.Cells operates with evaluation limits and adds watermark notices.",
            DocumentCategory.PowerPoint => "Evaluation Mode: Aspose.Slides operates with evaluation limits and adds watermark notices.",
            _ => "Evaluation Mode: Aspose libraries are operating without a valid license. Extracted content is subject to evaluation limits and watermarks."
        };

        public string GetEvaluationNotice(string engineId)
        {
            if (string.IsNullOrWhiteSpace(engineId)) return string.Empty;
            if (engineId.Contains("pdf", StringComparison.OrdinalIgnoreCase))
            {
                return GetEvaluationNotice(DocumentCategory.Pdf);
            }
            if (engineId.Contains("word", StringComparison.OrdinalIgnoreCase))
            {
                return GetEvaluationNotice(DocumentCategory.Word);
            }
            if (engineId.Contains("cell", StringComparison.OrdinalIgnoreCase) || engineId.Contains("excel", StringComparison.OrdinalIgnoreCase))
            {
                return GetEvaluationNotice(DocumentCategory.Excel);
            }
            if (engineId.Contains("slide", StringComparison.OrdinalIgnoreCase) || engineId.Contains("powerpoint", StringComparison.OrdinalIgnoreCase))
            {
                return GetEvaluationNotice(DocumentCategory.PowerPoint);
            }
            return GetEvaluationNotice(DocumentCategory.PlainText);
        }

        public void InitializeLicenses(string? explicitLicensePath = null)
        {
            lock (SyncLock)
            {
                var licensePath = ResolveLicensePath(explicitLicensePath);
                if (string.IsNullOrEmpty(licensePath) || !File.Exists(licensePath))
                {
                    _isLicensed = false;
                    _isWordsLicensed = false;
                    _isCellsLicensed = false;
                    _isSlidesLicensed = false;
                    _isPdfLicensed = false;
                    _activeLicensePath = null;
                    return;
                }

                bool wordsSuccess = TryApplyWordsLicense(licensePath);
                bool cellsSuccess = TryApplyCellsLicense(licensePath);
                bool slidesSuccess = TryApplySlidesLicense(licensePath);
                bool pdfSuccess = TryApplyPdfLicense(licensePath);

                _isWordsLicensed = wordsSuccess;
                _isCellsLicensed = cellsSuccess;
                _isSlidesLicensed = slidesSuccess;
                _isPdfLicensed = pdfSuccess;

                if (wordsSuccess || cellsSuccess || slidesSuccess || pdfSuccess)
                {
                    _isLicensed = true;
                    _activeLicensePath = licensePath;
                }
                else
                {
                    _isLicensed = false;
                    _activeLicensePath = null;
                }
            }
        }

        private static string? ResolveLicensePath(string? explicitPath)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            {
                return explicitPath;
            }

            var envPath = Environment.GetEnvironmentVariable("ASPOSE_LICENSE_PATH");
            if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
            {
                return envPath;
            }

            // Probe application directory and user data directory
            var searchDirectories = new[]
            {
                AppDomain.CurrentDomain.BaseDirectory,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileFormatAIStudio")
            };

            var candidateFileNames = new[]
            {
                "Aspose.Total.NET.lic",
                "Aspose.Total.lic",
                "Aspose.Words.lic",
                "Aspose.lic"
            };

            foreach (var dir in searchDirectories)
            {
                if (!Directory.Exists(dir)) continue;

                foreach (var fileName in candidateFileNames)
                {
                    var fullPath = Path.Combine(dir, fileName);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
            }

            return null;
        }

        private static bool TryApplyWordsLicense(string licensePath)
        {
            try
            {
                var license = new Aspose.Words.License();
                license.SetLicense(licensePath);
                return true;
            }
            catch
            {
                // Fallback to evaluation mode
                return false;
            }
        }

        private static bool TryApplyCellsLicense(string licensePath)
        {
            try
            {
                var license = new Aspose.Cells.License();
                license.SetLicense(licensePath);
                return true;
            }
            catch
            {
                // Fallback to evaluation mode
                return false;
            }
        }

        private static bool TryApplySlidesLicense(string licensePath)
        {
            try
            {
                var license = new Aspose.Slides.License();
                license.SetLicense(licensePath);
                return true;
            }
            catch
            {
                // Fallback to evaluation mode
                return false;
            }
        }

        private static bool TryApplyPdfLicense(string licensePath)
        {
            try
            {
                var license = new Aspose.Pdf.License();
                license.SetLicense(licensePath);
                return true;
            }
            catch
            {
                // Fallback to evaluation mode
                return false;
            }
        }
    }
}

