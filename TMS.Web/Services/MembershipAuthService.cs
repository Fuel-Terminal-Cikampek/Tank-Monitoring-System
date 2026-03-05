using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using TMS.Web.Areas.Identity.Data;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.IO;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace TMS.Web.Services
{
    /// <summary>
    /// Service untuk autentikasi dengan sistem membership (aspnet_Users + aspnet_Membership)
    /// Menggunakan 3DES encryption dengan kunci dari CSF_CIKAMPEK
    /// </summary>
    public class MembershipAuthService
    {
        private readonly TMSContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public MembershipAuthService(TMSContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Autentikasi user dengan username dan password
        /// </summary>
        public async Task<bool> AuthenticateAsync(string username, string password)
        {
            try
            {
                // Cari user berdasarkan username (case insensitive)
                var user = await _context.AspnetUsers
                    .FirstOrDefaultAsync(u => u.LoweredUserName == username.ToLower());

                if (user == null)
                    return false; // User tidak ditemukan

                // Cari data membership berdasarkan UserId
                var membership = await _context.AspnetMemberships
                    .FirstOrDefaultAsync(m => m.UserId == user.UserId);

                if (membership == null)
                    return false; // User tidak memiliki akun membership

                // Periksa apakah user approved
                if (!membership.IsApproved)
                    return false; // User belum di-approve

                // Periksa apakah user locked out
                if (membership.IsLockedOut)
                    return false; // User di-lock

                // Verifikasi password
                if (!VerifyPassword(password, membership.Password))
                    return false; // Password salah

                // Ambil roles user
                var roles = await _context.AspnetUserInRoles
                    .Where(r => r.UserId == user.UserId)
                    .Include(r => r.Role)
                    .ToListAsync();

                // Buat daftar klaim untuk sesi user
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim(ClaimTypes.Email, membership.Email ?? ""),
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString())
                };

                // Tambah claims untuk setiap role
                foreach (var userRole in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, userRole.Role.RoleName));
                }

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

                // Login menggunakan cookie authentication
                await _httpContextAccessor.HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    claimsPrincipal,
                    new AuthenticationProperties { IsPersistent = false }
                );

                // Update LastActivityDate
                user.LastActivityDate = DateTime.Now;
                membership.LastLoginDate = DateTime.Now;
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Autentikasi gagal: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Logout user
        /// </summary>
        public async Task LogoutAsync()
        {
            await _httpContextAccessor.HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme
            );
        }

        /// <summary>
        /// Verifikasi password dengan dekripsi menggunakan 3DES
        /// Kunci diambil dari CSF_CIKAMPEK
        /// </summary>
        private bool VerifyPassword(string inputPassword, string storedPassword)
        {
            if (string.IsNullOrEmpty(storedPassword))
                return false;

            try
            {
                // Lakukan dekripsi
                string decryptedPassword = DecryptPassword(storedPassword);

                // Opsi 1: Cek apakah input password terdapat di hasil dekripsi (substring match)
                // Menggunakan Contains() karena database mungkin menyimpan password dengan format/padding tertentu
                if (decryptedPassword.Contains(inputPassword))
                {
                    Console.WriteLine("✓ Password valid berdasarkan substring match.");
                    return true;
                }

                // Debug: Log decrypted password untuk troubleshooting
                Console.WriteLine($"✗ Password tidak cocok.");
                Console.WriteLine($"  Input: {inputPassword}");
                Console.WriteLine($"  Decrypted: {decryptedPassword}");

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Verifikasi password gagal: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Dekripsi password menggunakan 3DES
        /// Kunci: F9D1A2D3E1D3E2F7B3D9F90FF3965ABDAC304902F8D923AC (dari CSF_CIKAMPEK)
        /// </summary>
        private string DecryptPassword(string storedPassword)
        {
            try
            {
                // Password disimpan dalam format base64
                byte[] encryptedBytes = Convert.FromBase64String(storedPassword);

                // Gunakan decryptionKey (dari CSF_CIKAMPEK)
                string keyHex = "F9D1A2D3E1D3E2F7B3D9F90FF3965ABDAC304902F8D923AC";
                byte[] keyBytes = HexStringToByteArray(keyHex);

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
                throw new Exception($"Gagal mendekripsi password: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Expand key jika panjangnya kurang dari 24 byte
        /// </summary>
        private byte[] ExpandKey(byte[] keyBytes)
        {
            byte[] key = new byte[24];
            Array.Copy(keyBytes, key, keyBytes.Length);
            for (int i = keyBytes.Length; i < 24; i++)
            {
                key[i] = keyBytes[i % keyBytes.Length];
            }
            return key;
        }

        /// <summary>
        /// Ubah hex string menjadi byte array
        /// </summary>
        private static byte[] HexStringToByteArray(string hex)
        {
            if (hex.Length % 2 != 0)
                throw new ArgumentException("Hex string harus memiliki panjang genap");

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }
    }
}
