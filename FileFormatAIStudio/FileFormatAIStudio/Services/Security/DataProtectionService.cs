using System;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace FileFormatAIStudio.Services.Security
{
    [SupportedOSPlatform("windows")]
    public static class DataProtectionService
    {
        private const string DpapiPrefix = "dpapi:";

        /// <summary>
        /// Encrypts plaintext using Windows DPAPI (CurrentUser scope) and returns a prefixed Base64 string.
        /// If input is empty, returns empty string.
        /// If already encrypted with the prefix, returns as is.
        /// </summary>
        public static string Protect(string? plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return string.Empty;
            }

            if (plainText.StartsWith(DpapiPrefix, StringComparison.Ordinal))
            {
                return plainText;
            }

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] cipherBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
                return DpapiPrefix + Convert.ToBase64String(cipherBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataProtectionService] Failed to protect data: {ex.Message}");
                // In the unlikely event DPAPI fails, return the original plaintext rather than losing user configuration
                return plainText;
            }
        }

        /// <summary>
        /// Decrypts a DPAPI-encrypted string.
        /// If the string does not have the "dpapi:" prefix, it is treated as a legacy plaintext key and returned as is.
        /// If decryption fails (e.g. data transferred from another machine or user), falls back gracefully.
        /// </summary>
        public static string Unprotect(string? cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
            {
                return string.Empty;
            }

            // Backward compatibility: If it doesn't start with dpapi:, it's a legacy unencrypted key
            if (!cipherText.StartsWith(DpapiPrefix, StringComparison.Ordinal))
            {
                return cipherText;
            }

            try
            {
                string base64Payload = cipherText.Substring(DpapiPrefix.Length);
                byte[] cipherBytes = Convert.FromBase64String(base64Payload);
                byte[] plainBytes = ProtectedData.Unprotect(cipherBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DataProtectionService] Failed to unprotect data: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
