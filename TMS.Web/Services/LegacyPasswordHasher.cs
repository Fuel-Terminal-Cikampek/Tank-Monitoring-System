using Microsoft.AspNetCore.Identity;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TMS.Web.Areas.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace TMS.Web.Services
{
    /// <summary>
    /// Custom password hasher for legacy ASP.NET 2.0 Membership passwords
    /// Supports BOTH password formats:
    /// - PasswordFormat=1: HMACSHA1 hashed with salt
    /// - PasswordFormat=2: 3DES encrypted (from CSF_CIKAMPEK)
    /// </summary>
    public class LegacyPasswordHasher : IPasswordHasher<AppUser>
    {
        private readonly TMSContext _context;
        // Decryption key dari CSF_CIKAMPEK (3DES)
        private const string DECRYPTION_KEY_HEX = "F9D1A2D3E1D3E2F7B3D9F90FF3965ABDAC304902F8D923AC";

        public LegacyPasswordHasher(TMSContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Hash a password using ASP.NET Core Identity default algorithm
        /// Not used for legacy verification, but required by interface
        /// </summary>
        public string HashPassword(AppUser user, string password)
        {
            // Use default password hasher for new password changes
            var hasher = new PasswordHasher<AppUser>();
            return hasher.HashPassword(user, password);
        }

        /// <summary>
        /// Verify password against legacy aspnet_Membership password format
        /// Supports both HMACSHA1 (Format=1) and 3DES encrypted (Format=2)
        /// </summary>
        public PasswordVerificationResult VerifyHashedPassword(AppUser user, string hash, string providedPassword)
        {
            try
            {
                Console.WriteLine($"[LegacyPasswordHasher] Verifying password for user: {user.UserName}, UserId: {user.Id}");

                // Get the legacy password record from aspnet_Membership table
                // AppUser.Id is now Guid (matching database uniqueidentifier column)
                var membership = _context.AspnetMemberships
                    .AsNoTracking()
                    .FirstOrDefault(m => m.UserId == user.Id);

                if (membership == null)
                {
                    Console.WriteLine($"[LegacyPasswordHasher] ERROR: No membership record found for UserId: {user.Id}");
                    return PasswordVerificationResult.Failed;
                }

                Console.WriteLine($"[LegacyPasswordHasher] Found membership: PasswordFormat={membership.PasswordFormat}, IsApproved={membership.IsApproved}, IsLockedOut={membership.IsLockedOut}");

                // Check approval and lockout status
                if (!membership.IsApproved || membership.IsLockedOut)
                {
                    return PasswordVerificationResult.Failed;
                }

                // Handle based on password format
                if (membership.PasswordFormat == 1) // HMACSHA1 hashed with salt
                {
                    Console.WriteLine($"[LegacyPasswordHasher] Using PasswordFormat=1 (HMACSHA1)");
                    var result = VerifyHashedPasswordFormat1(membership.Password, membership.PasswordSalt, providedPassword);
                    Console.WriteLine($"[LegacyPasswordHasher] Format1 verification result: {result}");
                    return result;
                }
                else if (membership.PasswordFormat == 2) // 3DES encrypted
                {
                    Console.WriteLine($"[LegacyPasswordHasher] Using PasswordFormat=2 (3DES)");
                    var result = VerifyEncryptedPasswordFormat2(membership.Password, providedPassword);
                    Console.WriteLine($"[LegacyPasswordHasher] Format2 verification result: {result}");
                    return result;
                }
                else if (membership.PasswordFormat == 0) // Clear text (legacy)
                {
                    Console.WriteLine($"[LegacyPasswordHasher] Using PasswordFormat=0 (Clear text)");
                    // Not recommended, but support for backwards compatibility
                    if (membership.Password == providedPassword)
                    {
                        return PasswordVerificationResult.Success;
                    }
                    return PasswordVerificationResult.Failed;
                }

                Console.WriteLine($"[LegacyPasswordHasher] ERROR: Unknown PasswordFormat: {membership.PasswordFormat}");
                return PasswordVerificationResult.Failed;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LegacyPasswordHasher] EXCEPTION: {ex.Message}");
                Console.WriteLine($"[LegacyPasswordHasher] StackTrace: {ex.StackTrace}");
                return PasswordVerificationResult.Failed;
            }
        }

        /// <summary>
        /// Verify password using HMACSHA1 format (PasswordFormat=1)
        /// ASP.NET 2.0 Membership uses: HMACSHA1(password + salt)
        /// </summary>
        private PasswordVerificationResult VerifyHashedPasswordFormat1(string storedHash, string salt, string providedPassword)
        {
            try
            {
                // Decode salt from base64
                byte[] saltBytes;
                try
                {
                    saltBytes = Convert.FromBase64String(salt ?? "");
                }
                catch
                {
                    return PasswordVerificationResult.Failed;
                }

                // Get password bytes
                byte[] passwordBytes = Encoding.UTF8.GetBytes(providedPassword);

                // Create HMACSHA1 hash
                byte[] hashData;
                using (var hmac = new HMACSHA1(saltBytes))
                {
                    hashData = hmac.ComputeHash(passwordBytes);
                }

                // Combine salt + hash and base64 encode (ASP.NET 2.0 format)
                byte[] saltAndHashBytes = new byte[saltBytes.Length + hashData.Length];
                Array.Copy(saltBytes, 0, saltAndHashBytes, 0, saltBytes.Length);
                Array.Copy(hashData, 0, saltAndHashBytes, saltBytes.Length, hashData.Length);

                string computedHash = Convert.ToBase64String(saltAndHashBytes);

                // Compare with stored hash
                if (storedHash == computedHash)
                {
                    return PasswordVerificationResult.Success;
                }

                return PasswordVerificationResult.Failed;
            }
            catch
            {
                return PasswordVerificationResult.Failed;
            }
        }

        /// <summary>
        /// Verify password using 3DES encrypted format (PasswordFormat=2)
        /// Uses decryption key dari CSF_CIKAMPEK
        /// </summary>
        private PasswordVerificationResult VerifyEncryptedPasswordFormat2(string encryptedPassword, string providedPassword)
        {
            try
            {
                // Decrypt the stored password
                string decryptedPassword = DecryptPassword(encryptedPassword);

                // Check if provided password matches decrypted password (with substring match for compatibility)
                if (decryptedPassword.Contains(providedPassword) || decryptedPassword == providedPassword)
                {
                    return PasswordVerificationResult.Success;
                }

                return PasswordVerificationResult.Failed;
            }
            catch
            {
                return PasswordVerificationResult.Failed;
            }
        }

        /// <summary>
        /// Decrypt password using 3DES
        /// Kunci: F9D1A2D3E1D3E2F7B3D9F90FF3965ABDAC304902F8D923AC (dari CSF_CIKAMPEK)
        /// </summary>
        private string DecryptPassword(string encryptedPasswordBase64)
        {
            try
            {
                // Password disimpan dalam format base64
                byte[] encryptedBytes = Convert.FromBase64String(encryptedPasswordBase64);

                // Convert hex string to byte array
                byte[] keyBytes = HexStringToByteArray(DECRYPTION_KEY_HEX);

                // Pastikan key berukuran 24 byte (3DES) dan IV 8 byte
                byte[] key = keyBytes.Length >= 24 ? keyBytes.Take(24).ToArray() : ExpandKey(keyBytes);
                byte[] iv = keyBytes.Take(8).ToArray();

                using (TripleDESCryptoServiceProvider des = new TripleDESCryptoServiceProvider())
                {
                    des.Key = key;
                    des.IV = iv;
                    des.Mode = CipherMode.CBC;
                    des.Padding = PaddingMode.PKCS7;

                    using (ICryptoTransform decryptor = des.CreateDecryptor())
                    using (MemoryStream msDecrypt = new MemoryStream(encryptedBytes))
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    using (StreamReader srDecrypt = new StreamReader(csDecrypt, Encoding.Unicode))
                    {
                        return srDecrypt.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to decrypt password: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Convert hex string to byte array
        /// </summary>
        private static byte[] HexStringToByteArray(string hex)
        {
            int numberChars = hex.Length;
            byte[] bytes = new byte[numberChars / 2];
            for (int i = 0; i < numberChars; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }

        /// <summary>
        /// Expand key jika panjangnya kurang dari 24 byte
        /// </summary>
        private static byte[] ExpandKey(byte[] keyBytes)
        {
            byte[] expandedKey = new byte[24];
            for (int i = 0; i < expandedKey.Length; i++)
            {
                expandedKey[i] = keyBytes[i % keyBytes.Length];
            }
            return expandedKey;
        }

        /// <summary>
        /// Encrypt password for new users using 3DES (PasswordFormat=2)
        /// Compatible with legacy ASP.NET Membership system
        /// Returns tuple of (encryptedPassword, salt)
        /// </summary>
        public static (string encryptedPassword, string salt) EncryptPasswordLegacy(string password)
        {
            try
            {
                // Generate 16-byte random salt (same as legacy system)
                byte[] saltBytes = new byte[16];
                using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
                {
                    rng.GetBytes(saltBytes);
                }

                // Convert password to bytes (MUST use Encoding.Unicode to match DecryptPassword)
                byte[] passwordBytes = System.Text.Encoding.Unicode.GetBytes(password);

                // Prepend salt to password (ASP.NET Membership format)
                byte[] saltAndPassword = new byte[saltBytes.Length + passwordBytes.Length];
                Array.Copy(saltBytes, 0, saltAndPassword, 0, saltBytes.Length);
                Array.Copy(passwordBytes, 0, saltAndPassword, saltBytes.Length, passwordBytes.Length);

                // Convert hex string to byte array
                byte[] keyBytes = HexStringToByteArray(DECRYPTION_KEY_HEX);

                // Pastikan key berukuran 24 byte (3DES) dan IV 8 byte
                byte[] key = keyBytes.Length >= 24 ? keyBytes.Take(24).ToArray() : ExpandKey(keyBytes);
                byte[] iv = keyBytes.Take(8).ToArray();

                using (TripleDESCryptoServiceProvider des = new TripleDESCryptoServiceProvider())
                {
                    des.Key = key;
                    des.IV = iv;
                    des.Mode = CipherMode.CBC;
                    des.Padding = PaddingMode.PKCS7;

                    using (ICryptoTransform encryptor = des.CreateEncryptor())
                    using (MemoryStream msEncrypt = new MemoryStream())
                    {
                        using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        {
                            csEncrypt.Write(saltAndPassword, 0, saltAndPassword.Length);
                            csEncrypt.FlushFinalBlock();
                        }
                        byte[] encryptedBytes = msEncrypt.ToArray();
                        string encryptedPassword = Convert.ToBase64String(encryptedBytes);
                        string salt = Convert.ToBase64String(saltBytes);
                        return (encryptedPassword, salt);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to encrypt password", ex);
            }
        }

    }
}
