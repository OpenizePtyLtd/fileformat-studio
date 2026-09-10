using System;
using System.IO;

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
        /// Gets the path to the currently active license file, if one was found and applied.
        /// </summary>
        string? ActiveLicensePath { get; }

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
        private string? _activeLicensePath;

        public bool IsLicensed => _isLicensed;
        public string? ActiveLicensePath => _activeLicensePath;

        public AsposeLicenseService()
        {
            InitializeLicenses();
        }

        public void InitializeLicenses(string? explicitLicensePath = null)
        {
            lock (SyncLock)
            {
                var licensePath = ResolveLicensePath(explicitLicensePath);
                if (string.IsNullOrEmpty(licensePath) || !File.Exists(licensePath))
                {
                    _isLicensed = false;
                    _activeLicensePath = null;
                    return;
                }

                bool wordsSuccess = TryApplyWordsLicense(licensePath);
                bool cellsSuccess = TryApplyCellsLicense(licensePath);
                bool slidesSuccess = TryApplySlidesLicense(licensePath);
                bool pdfSuccess = TryApplyPdfLicense(licensePath);

                if (wordsSuccess || cellsSuccess || slidesSuccess || pdfSuccess)
                {
                    _isLicensed = true;
                    _activeLicensePath = licensePath;
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

