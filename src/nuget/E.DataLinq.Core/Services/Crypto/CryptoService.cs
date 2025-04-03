using E.DataLinq.Core.Extensions;
using E.DataLinq.Core.Services.Crypto.Abstraction;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace E.DataLinq.Core.Services.Crypto;

public partial class CryptoService : ICryptoService
{
    private readonly CryptoServiceOptions _options;

    public CryptoService(IOptionsMonitor<CryptoServiceOptions> optionsMonitor)
    {
        _options = optionsMonitor.CurrentValue;
    }

    #region ICryptoService

    public string DecryptTextDefault(string input)
    {
        if (String.IsNullOrEmpty(input))
        {
            return String.Empty;
        }

        // Get the bytes of the string
        byte[] bytesToBeDecrypted = null;
        if (input.StartsWith("0x") && IsHexString(input))
        {
            bytesToBeDecrypted = StringToByteArray(input);
        }
        else
        {
            bytesToBeDecrypted = Convert.FromBase64String(input);
        }

        Exception decrytException = null;
        foreach (var options in _options.CurrentAndLegacy())
        {
            try
            {
                byte[] passwordBytes = Encoding.UTF8.GetBytes(options.DefaultPassword);
                passwordBytes = SHA256.Create().ComputeHash(passwordBytes);

                byte[] bytesDecrypted = AES_Decrypt(bytesToBeDecrypted, passwordBytes, GetKeySize(options.Strength), options.UseRandomSalt);

                string result = Encoding.UTF8.GetString(bytesDecrypted);
                if (result == "#string.empty#")
                {
                    return String.Empty;
                }

                return result;
            }
            catch (Exception ex)
            {
                decrytException ??= ex;
            }
        }

        throw decrytException ?? new Exception("Can't encrypt text");
    }

    public string EncryptTextDefault(string input)
    {
        return EncryptTextDefault(input, _options.DefaultResultType);
    }

    public string EncryptTextDefault(string input, CryptoResultStringType resultType)
    {
        if (String.IsNullOrEmpty(input))
        {
            input = "#string.empty#";
        }

        // Get the bytes of the string
        byte[] bytesToBeEncrypted = Encoding.UTF8.GetBytes(input);
        byte[] passwordBytes = Encoding.UTF8.GetBytes(_options.DefaultPassword);

        // Hash the password with SHA256
        passwordBytes = SHA256.Create().ComputeHash(passwordBytes);

        byte[] bytesEncrypted = AES_Encrypt(bytesToBeEncrypted, passwordBytes, GetKeySize(_options.Strength), _options.UseRandomSalt);

        string result = String.Empty;

        switch (resultType)
        {
            case CryptoResultStringType.Base64:
                result = Convert.ToBase64String(bytesEncrypted);
                break;
            case CryptoResultStringType.Hex:
                result = "0x" + string.Concat(bytesEncrypted.Select(b => b.ToString("X2")));
                break;
        }

        return result;
    }

    public string StaticEncryptDefault(string str, string password = "")
    {
        return StaticEncrypt(str, password, CryptoResultStringType.Hex);
    }
    public string StaticDecryptDefault(string str, string password = "")
    {
        return StaticDecrypt(str, password);
    }

    #endregion

    #region Helper

    private byte[] GetRandomBytes()
    {
        byte[] ba = new byte[_options.Saltsize];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(ba);
        }
        return ba;
    }

    private byte[] GetBytes(byte[] initialBytes, int size)
    {
        var ret = new byte[size];
        Buffer.BlockCopy(initialBytes, 0, ret, 0, Math.Min(initialBytes.Length, ret.Length));

        return ret;
    }

    private byte[] GetHashedBytes(byte[] initialBytes, int size)
    {
        var hash = SHA256.Create().ComputeHash(initialBytes);

        var ret = new byte[size];
        Buffer.BlockCopy(hash, 0, ret, 0, Math.Min(hash.Length, ret.Length));

        byte[] saltBytes = new byte[] { 176, 223, 23, 125, 64, 98, 177, 214 };
        var key = new Rfc2898DeriveBytes(
                    hash,
                    _options.HashBytesSalt,
                    10,
                    hashAlgorithm: HashAlgorithmName.SHA1);
        ret = key.GetBytes(size);

        return ret;
    }

    private int GetKeySize(CryptoStrength strength)
    {
        switch (strength)
        {
            case CryptoStrength.AES128:
                return 128;
            case CryptoStrength.AES192:
                return 192;
            case CryptoStrength.AES256:
                return 256;
        }

        return 128;
    }

    private bool IsHexString(string hex)
    {
        if (hex.StartsWith("0x"))
        {
            hex = hex.Substring(2, hex.Length - 2);
        }

        bool isHex;
        foreach (var c in hex)
        {
            isHex = ((c >= '0' && c <= '9') ||
                     (c >= 'a' && c <= 'f') ||
                     (c >= 'A' && c <= 'F'));

            if (!isHex)
            {
                return false;
            }
        }
        return true;
    }

    private byte[] StringToByteArray(String hex)
    {
        if (hex.StartsWith("0x"))
        {
            hex = hex.Substring(2, hex.Length - 2);
        }

        int NumberChars = hex.Length;
        byte[] bytes = new byte[NumberChars / 2];
        for (int i = 0; i < NumberChars; i += 2)
        {
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        }

        return bytes;
    }

    #region AES Base

    private byte[] AES_Encrypt(byte[] bytesToBeEncrypted, byte[] passwordBytes, int keySize = 128, bool useRandomSalt = true)
    {
        byte[] encryptedBytes = null;

        if (useRandomSalt)
        {
            // Add Random Salt in front -> two ident objects will produce differnt results
            // Remove the Bytes after decryption
            byte[] randomSalt = GetRandomBytes();
            byte[] bytesToEncrpytWidhSalt = new byte[randomSalt.Length + bytesToBeEncrypted.Length];
            Buffer.BlockCopy(randomSalt, 0, bytesToEncrpytWidhSalt, 0, randomSalt.Length);
            Buffer.BlockCopy(bytesToBeEncrypted, 0, bytesToEncrpytWidhSalt, randomSalt.Length, bytesToBeEncrypted.Length);

            bytesToBeEncrypted = bytesToEncrpytWidhSalt;
        }

        using (MemoryStream ms = new MemoryStream())
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = keySize;
                aes.BlockSize = 128;

                /*
                // Set your salt here, change it to meet your flavor:
                // The salt bytes must be at least 8 bytes.
                byte[] saltBytes = new byte[] { 176, 223, 23, 125, 64, 98, 177, 214 };
                  
                var key = new Rfc2898DeriveBytes(passwordBytes, saltBytes, _iterations);
                AES.Key = key.GetBytes(AES.KeySize / 8);
                AES.IV = key.GetBytes(AES.BlockSize / 8);
                 * */

                // Faster (store 4 bytes to generating IV...)
                byte[] ivInitialBytes = GetRandomBytes();
                ms.Write(ivInitialBytes, 0, _options.Saltsize);

                aes.Key = GetBytes(passwordBytes, aes.KeySize / 8);
                aes.IV = GetHashedBytes(ivInitialBytes, aes.BlockSize / 8);

                aes.Mode = CipherMode.CBC;

                using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(bytesToBeEncrypted, 0, bytesToBeEncrypted.Length);
                    cs.Close();
                }
                encryptedBytes = ms.ToArray();
            }
        }

        return encryptedBytes;
    }

    private byte[] AES_Decrypt(byte[] bytesToBeDecrypted, byte[] passwordBytes, int keySize = 128, bool useRandomSalt = true)
    {
        byte[] decryptedBytes = null;

        using (MemoryStream ms = new MemoryStream())
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = keySize;
                aes.BlockSize = 128;

                /*
                // Set your salt here, change it to meet your flavor:
                // The salt bytes must be at least 8 bytes.
                byte[] saltBytes = new byte[] { 176, 223, 23, 125, 64, 98, 177, 214 };
                 *
                var key = new Rfc2898DeriveBytes(passwordBytes, saltBytes, _iterations);
                AES.Key = key.GetBytes(AES.KeySize / 8);
                AES.IV = key.GetBytes(AES.BlockSize / 8);
                */

                // Faster get bytes for IV from 
                var ivInitialBytes = new byte[_options.Saltsize];
                Buffer.BlockCopy(bytesToBeDecrypted, 0, ivInitialBytes, 0, _options.Saltsize);

                aes.Key = GetBytes(passwordBytes, aes.KeySize / 8);
                aes.IV = GetHashedBytes(ivInitialBytes, aes.BlockSize / 8);

                aes.Mode = CipherMode.CBC;

                using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(bytesToBeDecrypted, _options.Saltsize, bytesToBeDecrypted.Length - _options.Saltsize);
                    cs.Close();
                }
                decryptedBytes = ms.ToArray();
            }
        }

        if (useRandomSalt)
        {
            byte[] ret = new byte[decryptedBytes.Length - _options.Saltsize];
            Buffer.BlockCopy(decryptedBytes, _options.Saltsize, ret, 0, ret.Length);
            decryptedBytes = ret;
        }

        return decryptedBytes;
    }

    #endregion

    #region Static Encryption

    private static byte[] _static_iv = new byte[8] { 10, 116, 99, 177, 149, 87, 42, 67 };

    //
    //  GetStaticPassword() is set in the Partial Class in CryptoImpl.HiddenSecrets.cs
    //  If its not compiling:
    //  Copy CryptoService.HiddenSecrets__.cs and rename it to CryptoService.HiddenScrets.cs. This file schould be ignored by GIT (.gitignore)
    //  Uncomment Code in CryptoService.HiddenSecrets.cs und set _static_defaultPassword for your development environment
    //

    public string StaticEncrypt(string text, string password, CryptoResultStringType resultStringType = CryptoResultStringType.Base64)
    {
        if (String.IsNullOrEmpty(password))
        {
            password = GetStaticPseudoKey();
        }

        byte[] passwordBytes = Encoding.UTF8.GetBytes(StaticPassword24(password));
        byte[] inputbuffer = Encoding.UTF8.GetBytes(text);

        SymmetricAlgorithm algorithm = System.Security.Cryptography.TripleDES.Create();
        ICryptoTransform transform = algorithm.CreateEncryptor(passwordBytes, _static_iv);

        var outputBuffer = transform.TransformFinalBlock(inputbuffer, 0, inputbuffer.Length);

        string result = String.Empty;
        switch (resultStringType)
        {
            case CryptoResultStringType.Base64:
                result = Convert.ToBase64String(outputBuffer);
                break;
            case CryptoResultStringType.Hex:
                result = "0x" + string.Concat(outputBuffer.Select(b => b.ToString("X2")));
                break;
        }

        return result;
    }

    private string StaticDecrypt(string input, string password)
    {
        if (String.IsNullOrEmpty(password))
        {
            password = GetStaticPseudoKey();
        }

        List<byte> inputbuffer = new List<byte>();

        if (input.StartsWith("0x") && IsHexString(input))
        {
            inputbuffer.AddRange(StringToByteArray(input));
        }
        else
        {
            inputbuffer.AddRange(Convert.FromBase64String(input));
        }

        byte[] passwordBytes = Encoding.UTF8.GetBytes(StaticPassword24(password));

        SymmetricAlgorithm algorithm = System.Security.Cryptography.TripleDES.Create();
        ICryptoTransform transform = algorithm.CreateDecryptor(passwordBytes, _static_iv);
        byte[] bytesDecrypted = transform.TransformFinalBlock(inputbuffer.ToArray(), 0, inputbuffer.Count);

        string result = Encoding.UTF8.GetString(bytesDecrypted);

        if (result == "#string.emtpy#")
        {
            return String.Empty;
        }

        return result;
    }

    private string StaticPassword24(string password)
    {
        if (String.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Invalid password");
        }
        while (password.Length < 24)
        {
            password += password;
        }

        return password.Substring(0, 24);
    }

    #endregion

    private string Hash64_SHA1(byte[] passwordBytes)
    {
        using (var algorithm = SHA1.Create())
        {
            passwordBytes = algorithm.ComputeHash(passwordBytes);
        }

        return Convert.ToBase64String(passwordBytes);
    }

    #region Static Pseudo Key

    // generate e pseudo Password for the server side encryption
    private static string __static_defaultKey = "";

    private string GetStaticPseudoKey()
    {
        if (string.IsNullOrEmpty(__static_defaultKey))
        {
            var ob = new Obsucured();
            var vault = new Standard.Vault.KeyVault(ob.GetBase64());

            __static_defaultKey = vault.DecryptKey(ob.Id, ob.GetGuid());
        }

        return __static_defaultKey;
    }

    private class Obsucured
    {
        private byte[] k1 = new byte[]
        {
            52, 126, 107, 50,
            119, 93, 41, 50, 43, 90, 51, 94,
            117, 46, 56, 54, 55, 70, 111, 75, 62, 51, 83,
            78, 102, 56, 55, 88, 45, 125, 61, 68
        };

        private byte[] k2 = new byte[]
        {
            151,110,6,180,
            206,25,170,76,154,
            28,86,111,233,11,120,199
        };

        internal string Id => "DataLinq";
        internal string GetBase64() => Convert.ToBase64String(k1);
        internal Guid GetGuid() => new Guid(k2);
    }

    #endregion

    #endregion
}
