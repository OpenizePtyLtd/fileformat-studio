using System;
using System.IO;
using FileFormatAIStudio.Services.Benchmarking;
using FileFormatAIStudio.Services.Parsing;
using FluentAssertions;
using Xunit;

namespace FileFormatAIStudio.Tests
{
    public class AsposeLicenseServiceTests
    {
        [Fact]
        public void InitialState_OperatesInEvaluationMode_WhenNoLicensePresent()
        {
            var service = new AsposeLicenseService();

            if (service.ActiveLicensePath == null)
            {
                service.IsLicensed.Should().BeFalse();
                service.IsPdfLicensed.Should().BeFalse();
                service.IsWordsLicensed.Should().BeFalse();
                service.IsCellsLicensed.Should().BeFalse();
                service.IsSlidesLicensed.Should().BeFalse();
            }
        }

        [Fact]
        public void IsCategoryLicensed_UnlicensedState_ReturnsFalseForOfficeAndTrueForPlainText()
        {
            var service = new AsposeLicenseService();
            service.InitializeLicenses("C:\\NonExistent\\license.lic");

            service.IsCategoryLicensed(DocumentCategory.Pdf).Should().BeFalse();
            service.IsCategoryLicensed(DocumentCategory.Word).Should().BeFalse();
            service.IsCategoryLicensed(DocumentCategory.Excel).Should().BeFalse();
            service.IsCategoryLicensed(DocumentCategory.PowerPoint).Should().BeFalse();
            service.IsCategoryLicensed(DocumentCategory.PlainText).Should().BeTrue();
        }

        [Fact]
        public void IsEngineLicensed_DistinguishesAsposeVsNonAspose()
        {
            var service = new AsposeLicenseService();
            service.InitializeLicenses("C:\\NonExistent\\license.lic");

            // Non-Aspose engines never require an Aspose license
            service.IsEngineLicensed("pdfpig").Should().BeTrue();
            service.IsEngineLicensed("openxml").Should().BeTrue();
            service.IsEngineLicensed("exceldatareader").Should().BeTrue();
            service.IsEngineLicensed("officeparser-pdf").Should().BeTrue();
            service.IsEngineLicensed("plaintext").Should().BeTrue();

            // Aspose engines require licenses
            service.IsEngineLicensed("aspose-pdf").Should().BeFalse();
            service.IsEngineLicensed("aspose-words").Should().BeFalse();
            service.IsEngineLicensed("aspose-cells").Should().BeFalse();
            service.IsEngineLicensed("aspose-slides").Should().BeFalse();
            service.IsEngineLicensed("aspose").Should().BeFalse();
        }

        [Fact]
        public void GetEvaluationNotice_ReturnsAccurateLimitationNotices()
        {
            var service = new AsposeLicenseService();

            string pdfNotice = service.GetEvaluationNotice(DocumentCategory.Pdf);
            pdfNotice.Should().Contain("4 pages");
            pdfNotice.Should().Contain("Evaluation Mode");

            string wordNotice = service.GetEvaluationNotice(DocumentCategory.Word);
            wordNotice.Should().Contain("watermark");

            string excelNotice = service.GetEvaluationNotice(DocumentCategory.Excel);
            excelNotice.Should().Contain("evaluation");

            string slidesNotice = service.GetEvaluationNotice(DocumentCategory.PowerPoint);
            slidesNotice.Should().Contain("evaluation");

            string enginePdfNotice = service.GetEvaluationNotice("aspose-pdf");
            enginePdfNotice.Should().Be(pdfNotice);

            string engineWordNotice = service.GetEvaluationNotice("aspose-words");
            engineWordNotice.Should().Be(wordNotice);
        }

        [Fact]
        public void InitializeLicenses_NonExistentFile_GracefullyRemainsEvaluationMode()
        {
            var service = new AsposeLicenseService();
            service.InitializeLicenses(@"C:\Path\To\FakeLicense_" + Guid.NewGuid() + ".lic");

            service.IsLicensed.Should().BeFalse();
            service.IsPdfLicensed.Should().BeFalse();
            service.ActiveLicensePath.Should().BeNull();
        }

        [Fact]
        public void InstallLicense_CopiesFileToTargetDirectory()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"aspose_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var service = new AsposeLicenseService
                {
                    TargetLicenseDirectory = tempDir
                };

                var sourceFile = Path.Combine(tempDir, "SourceLicense.lic");
                File.WriteAllText(sourceFile, "<License><Data></Data></License>");

                // Attempt install - file will be copied to TargetLicensePath
                service.InstallLicense(sourceFile);

                File.Exists(service.TargetLicensePath).Should().BeTrue();
                File.ReadAllText(service.TargetLicensePath).Should().Be("<License><Data></Data></License>");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public void InstallLicense_NonExistentSourceFile_ReturnsFalse()
        {
            var service = new AsposeLicenseService();
            bool result = service.InstallLicense(@"C:\NonExistent\Missing_" + Guid.NewGuid() + ".lic");
            result.Should().BeFalse();
        }

        [Fact]
        public void RemoveLicense_DeletesLicenseFilesAndResetsState()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"aspose_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var service = new AsposeLicenseService
                {
                    TargetLicenseDirectory = tempDir
                };

                File.WriteAllText(service.TargetLicensePath, "test license content");
                File.Exists(service.TargetLicensePath).Should().BeTrue();

                bool removed = service.RemoveLicense();
                removed.Should().BeTrue();
                File.Exists(service.TargetLicensePath).Should().BeFalse();
                service.IsLicensed.Should().BeFalse();
                service.IsWordsLicensed.Should().BeFalse();
                service.ActiveLicensePath.Should().BeNull();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}

