using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Net.NetworkInformation;
using System.Windows.Forms;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

namespace Girt_PurlinAutoConnection
{
    internal class Licensing
    {
        // LicenseAdmin ile birebir aynı olmalı
        private const string Pepper = "m3m3nto-qr_pepper_v1";

        // TODO: BURAYA LicenseAdmin'de üretilen ecdsa-public.pem içeriğini YAPIŞTIR
        private const string PublicKeyPem = @"
-----BEGIN PUBLIC KEY-----
MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEmX+PKT68q7ndRtIwstl0qeMxrqdJ
/sCoNUMgNbtlTSICQe3BQH80bABA7J8BDBlQZL8nOQKlxRGQirIsjLBSLw==
-----END PUBLIC KEY-----";

        // Config dosyası: %ProgramData%\YourCompany\YourApp\config.json
        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "YourCompany", "YourApp");
        private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

        // Lisans dosyası modeli (JSON)
        private sealed class LicenseDoc
        {
            public string product { get; set; }
            public int version { get; set; }
            public string issued_at_utc { get; set; }
            public string expires_at_utc { get; set; }
            public string[] mac_hashes { get; set; }

            [JsonPropertyName("plain_macs")]
            public string[] PlainMacs { get; set; }

            public string signature { get; set; }
        }

        private sealed class AppConfig
        {
            [JsonPropertyName("license_path")]
            public string LicensePath { get; set; }
        }

        /// <summary>
        /// Uygulama açılışında çağırın. Lisansı doğrular; başarısızsa MessageBox gösterir.
        /// </summary>
        internal static bool EnsureAuthorized(IWin32Window owner = null)
        {
            // 0) Public key PEM kontrol (BC ile)
            try
            {
                using (var sr = new System.IO.StringReader(PublicKeyPem))
                {
                    var pr = new Org.BouncyCastle.OpenSsl.PemReader(sr);
                    var obj = pr.ReadObject();
                    Org.BouncyCastle.Crypto.AsymmetricKeyParameter pub = null;
                    if (obj is Org.BouncyCastle.Crypto.AsymmetricCipherKeyPair kp) pub = kp.Public;
                    else if (obj is Org.BouncyCastle.Crypto.AsymmetricKeyParameter pk) pub = pk;
                    if (pub == null || pub.IsPrivate)
                    {
                        MessageBox.Show("PublicKeyPem geçerli bir PUBLIC KEY içermiyor.",
                                        "License Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Public key PEM okunamadı: " + ex.Message,
                                "License Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            // 1) Sabit lisans yolunu kullan (HİÇ SORMADAN)
            var licPath = FixedLicensePath;

            if (!File.Exists(licPath))
            {
                MessageBox.Show("Lisans dosyası bulunamadı:\n" + licPath,
                                "License Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            // 2) Doğrula
            if (!VerifyLicenseFileVerbose(licPath, out _, out _))
            {
                MessageBox.Show("License verification failed.\nBu cihaz yetkili değil veya imza geçersiz.",
                                "License Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            return true;
        }

        // Lisans dosyasının sabit yolu 
        private const string FixedLicensePath = @"C:\ProgramData\BCASOFT\Girt-PurlinAutoConnection\license.lic";
        // Örn: @"D:\Licenses\MainApp\license.lic" veya ağ yolu: @"\\server\share\license.lic"

        // ------------------ İç İşler ------------------

        private enum LicenseFailReason { None, PublicKeyInvalid, LicenseFileRead, SignatureInvalid, Expired, NoMacMatch }

        private static bool VerifyLicenseFileVerbose(string path, out LicenseFailReason reason, out string detail)
        {
            reason = LicenseFailReason.None;
            detail = "";
            try
            {
                Log("Reading license file...");
                var json = File.ReadAllText(path, Encoding.UTF8);
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var lic = JsonSerializer.Deserialize<LicenseDoc>(json, opts);
                if (lic == null || lic.mac_hashes == null || string.IsNullOrEmpty(lic.signature))
                {
                    reason = LicenseFailReason.LicenseFileRead;
                    detail = "JSON null veya gerekli alanlar eksik.";
                    Log(detail);
                    return false;
                }

                var payload = new
                {
                    product = lic.product,
                    version = lic.version,
                    issued_at_utc = lic.issued_at_utc,
                    expires_at_utc = lic.expires_at_utc,
                    mac_hashes = lic.mac_hashes,
                    plain_macs = lic.PlainMacs
                };
                var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });
                var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
                Log("Payload len=" + payloadJson.Length);

                // İmza doğrulama (BC + .NET; herhangi biri True ise kabul)
                var sigBytes = FromBase64Url(lic.signature);

                bool sigOkBC = false, sigOkNet = false;
                try { sigOkBC = VerifySignatureWithPem(PublicKeyPem, payloadBytes, sigBytes); } catch { /* yut */ }
                try { sigOkNet = VerifySignatureWithPem_DotNet(PublicKeyPem, payloadBytes, sigBytes); } catch { /* yut */ }

                // (İsteğe bağlı debug)
                // MessageBox.Show($"Signature OK?  BC={sigOkBC}  NET={sigOkNet}");

                if (!(sigOkBC || sigOkNet))
                {
                    reason = LicenseFailReason.SignatureInvalid;
                    detail = "ECDSA (P-256) imzası doğrulanamadı. Public–private çifti ve imzalanan payload'ı kontrol edin.";
                    return false;
                }


                if (!string.IsNullOrEmpty(lic.expires_at_utc) &&
                    DateTime.TryParse(lic.expires_at_utc, out var expUtc) &&
                    DateTime.UtcNow > expUtc)
                {
                    reason = LicenseFailReason.Expired;
                    detail = $"Lisans süresi dolmuş. Exp={expUtc:o}, Now={DateTime.UtcNow:o}";
                    Log(detail);
                    return false;
                }

                var localMacs = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic =>
                        nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                        nic.GetPhysicalAddress().GetAddressBytes().Length == 6)
                    .Select(nic => NormalizeMacFromHex(nic.GetPhysicalAddress().ToString()))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();


                var localHashes = localMacs.Select(mac => Sha256Hex(mac + "|" + Pepper)).ToArray();

                Log("Local MACs: " + string.Join(", ", localMacs));
                Log("Local hashes first16: " + string.Join(", ", localHashes.Select(h => h.Substring(0, Math.Min(16, h.Length)))));
                Log("Licensed hashes first16: " + string.Join(", ", lic.mac_hashes.Select(h => h.Substring(0, Math.Min(16, h.Length)))));

                bool match = localHashes.Any(h => lic.mac_hashes.Contains(h, StringComparer.OrdinalIgnoreCase));
                Log("Any MAC match? " + match);

                if (!match)
                {
                    reason = LicenseFailReason.NoMacMatch;
                    detail = "Bu cihazdaki aktif fiziksel adaptörlerde lisanslanan MAC bulunamadı.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                reason = LicenseFailReason.LicenseFileRead;
                detail = ex.ToString();
                Log("VerifyLicenseFileVerbose EX: " + ex);
                return false;
            }
        }

        private static string NormalizeMacFromHex(string hex12)
        {
            if (string.IsNullOrWhiteSpace(hex12) || hex12.Length != 12)
                throw new ArgumentException("Bad MAC");
            return string.Join(":", Enumerable.Range(0, 6).Select(i => hex12.Substring(i * 2, 2))).ToUpperInvariant();
        }

        private static string Sha256Hex(string text)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                // .NET Framework uyumluluğu için:
                return BitConverter.ToString(bytes).Replace("-", ""); // AABBCC...
            }
        }

        private static byte[] FromBase64Url(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }
            return Convert.FromBase64String(s);
        }

        private static bool VerifySignatureWithPem(string publicKeyPem, byte[] data, byte[] signature)
        {
            using (var sr = new StringReader(publicKeyPem))
            {
                var pr = new PemReader(sr);
                var obj = pr.ReadObject();

                // Public key'i çıkar
                AsymmetricKeyParameter publicKey = null;
                if (obj is AsymmetricCipherKeyPair kp)
                    publicKey = kp.Public;
                else if (obj is AsymmetricKeyParameter pk)
                    publicKey = pk;
                else
                    throw new Exception("Invalid EC public key PEM");

                var verifier = SignerUtilities.GetSigner("SHA-256withECDSA");
                verifier.Init(false, publicKey);
                verifier.BlockUpdate(data, 0, data.Length);
                return verifier.VerifySignature(signature);
            }
        }

        private static readonly string DebugLogPath =
            Path.Combine(ConfigDir, "license_debug.log");

        private static void Log(string msg)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                File.AppendAllText(DebugLogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}{Environment.NewLine}",
                    Encoding.UTF8);
            }
            catch { /* log yazılamazsa sessiz geç */ }
        }

        private static bool VerifySignatureWithPem_DotNet(string publicKeyPem, byte[] data, byte[] signatureDer)
        {
            try
            {
                using (var sr = new System.IO.StringReader(publicKeyPem))
                {
                    var pr = new Org.BouncyCastle.OpenSsl.PemReader(sr);
                    var obj = pr.ReadObject();

                    Org.BouncyCastle.Crypto.AsymmetricKeyParameter pk =
                        (obj is Org.BouncyCastle.Crypto.AsymmetricCipherKeyPair kp) ? kp.Public
                                                                                    : (Org.BouncyCastle.Crypto.AsymmetricKeyParameter)obj;

                    // BC → .NET ECParameters
                    var ecp = (Org.BouncyCastle.Crypto.Parameters.ECPublicKeyParameters)pk;
                    var q = ecp.Q.Normalize();
                    byte[] x = q.AffineXCoord.GetEncoded();
                    byte[] y = q.AffineYCoord.GetEncoded();

                    var ecParams = new System.Security.Cryptography.ECParameters
                    {
                        Curve = System.Security.Cryptography.ECCurve.NamedCurves.nistP256,
                        Q = new System.Security.Cryptography.ECPoint { X = x, Y = y }
                    };

                    using (var ecdsa = System.Security.Cryptography.ECDsa.Create(ecParams))
                    {
                        // DER imzayı aynen kullanıyoruz
                        return ecdsa.VerifyData(data, signatureDer, System.Security.Cryptography.HashAlgorithmName.SHA256);
                    }
                }
            }
            catch
            {
                return false;
            }
        }

    }
}
