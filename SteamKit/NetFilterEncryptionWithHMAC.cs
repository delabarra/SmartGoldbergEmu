using System;
using System.Security.Cryptography;

namespace SteamKit
{
internal sealed class NetFilterEncryptionWithHMAC
{
    private const int InitializationVectorLength = 16;
    private const int InitializationVectorRandomLength = 3;

    private readonly Aes _aes;
    private readonly byte[] _hmacSecret;

    public NetFilterEncryptionWithHMAC(byte[] sessionKey)
    {
        if (sessionKey.Length != 32)
            throw new ArgumentException("Session key must be 32 bytes.", nameof(sessionKey));

        _hmacSecret = new byte[16];
        Array.Copy(sessionKey, 0, _hmacSecret, 0, _hmacSecret.Length);

        _aes = Aes.Create();
        _aes.BlockSize = 128;
        _aes.KeySize = 256;
        _aes.Key = sessionKey;
    }

    public byte[] ProcessIncoming(byte[] data)
    {
        if (data == null || data.Length < InitializationVectorLength)
            throw new CryptographicException("Incoming payload is too short.");

        byte[] iv = DecryptEcb(data, 0, InitializationVectorLength);
        byte[] plainText = DecryptCbc(data, InitializationVectorLength, data.Length - InitializationVectorLength, iv);

        ValidateInitializationVector(plainText, iv);
        return plainText;
    }

    public int ProcessOutgoing(byte[] data, byte[] output)
    {
        byte[] iv = new byte[InitializationVectorLength];
        GenerateInitializationVector(data, iv);

        byte[] encryptedIv = EncryptEcb(iv);
        byte[] cipherText = EncryptCbc(data, iv);

        int totalLength = encryptedIv.Length + cipherText.Length;
        if (output.Length < totalLength)
            throw new ArgumentException("Output buffer is too small.", nameof(output));

        Buffer.BlockCopy(encryptedIv, 0, output, 0, encryptedIv.Length);
        Buffer.BlockCopy(cipherText, 0, output, encryptedIv.Length, cipherText.Length);

        return totalLength;
    }

    public int CalculateMaxEncryptedDataLength(int plaintextDataLength)
    {
        int blockSize = _aes.BlockSize / 8;
        int cipherTextSize = (plaintextDataLength + blockSize) / blockSize * blockSize;
        return InitializationVectorLength + cipherTextSize;
    }

    private void GenerateInitializationVector(byte[] plainText, byte[] iv)
    {
        int hashLength = InitializationVectorLength - InitializationVectorRandomLength;
        FillRandom(iv, hashLength, InitializationVectorRandomLength);

        int hmacBufferLength = plainText.Length + InitializationVectorRandomLength;
        byte[] hmacBuffer = new byte[hmacBufferLength];

        Buffer.BlockCopy(iv, hashLength, hmacBuffer, 0, InitializationVectorRandomLength);
        Buffer.BlockCopy(plainText, 0, hmacBuffer, InitializationVectorRandomLength, plainText.Length);

        using (var hmac = new HMACSHA1(_hmacSecret))
        {
            byte[] hashValue = hmac.ComputeHash(hmacBuffer, 0, hmacBufferLength);
            Buffer.BlockCopy(hashValue, 0, iv, 0, hashLength);
        }
    }

    private void ValidateInitializationVector(byte[] plainText, byte[] iv)
    {
        int hashLength = InitializationVectorLength - InitializationVectorRandomLength;
        int hmacBufferLength = plainText.Length + InitializationVectorRandomLength;
        byte[] hmacBuffer = new byte[hmacBufferLength];

        Buffer.BlockCopy(iv, hashLength, hmacBuffer, 0, InitializationVectorRandomLength);
        Buffer.BlockCopy(plainText, 0, hmacBuffer, InitializationVectorRandomLength, plainText.Length);

        using (var hmac = new HMACSHA1(_hmacSecret))
        {
            byte[] hashValue = hmac.ComputeHash(hmacBuffer, 0, hmacBufferLength);
            for (int i = 0; i < hashLength; i++)
            {
                if (hashValue[i] != iv[i])
                    throw new CryptographicException("HMAC from server did not match computed HMAC.");
            }
        }
    }

    private byte[] EncryptEcb(byte[] plaintext)
    {
        _aes.Mode = CipherMode.ECB;
        _aes.Padding = PaddingMode.None;

        using (var transform = _aes.CreateEncryptor())
        {
            return transform.TransformFinalBlock(plaintext, 0, plaintext.Length);
        }
    }

    private byte[] DecryptEcb(byte[] input, int offset, int count)
    {
        _aes.Mode = CipherMode.ECB;
        _aes.Padding = PaddingMode.None;

        using (var transform = _aes.CreateDecryptor())
        {
            return transform.TransformFinalBlock(input, offset, count);
        }
    }

    private byte[] EncryptCbc(byte[] plaintext, byte[] iv)
    {
        _aes.Mode = CipherMode.CBC;
        _aes.Padding = PaddingMode.PKCS7;
        _aes.IV = iv;

        using (var transform = _aes.CreateEncryptor())
        {
            return transform.TransformFinalBlock(plaintext, 0, plaintext.Length);
        }
    }

    private byte[] DecryptCbc(byte[] input, int offset, int count, byte[] iv)
    {
        _aes.Mode = CipherMode.CBC;
        _aes.Padding = PaddingMode.PKCS7;
        _aes.IV = iv;

        using (var transform = _aes.CreateDecryptor())
        {
            return transform.TransformFinalBlock(input, offset, count);
        }
    }

    private static void FillRandom(byte[] buffer, int offset, int count)
    {
        byte[] random = new byte[count];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(random);
        }
        Buffer.BlockCopy(random, 0, buffer, offset, count);
    }
}
}
