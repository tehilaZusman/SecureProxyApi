using System.Security.Cryptography;

namespace SecureProxy.Bl.Security;

public sealed class AesGcmPayloadCryptoService : IPayloadCryptoService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly IKeySource _keySource;

    public AesGcmPayloadCryptoService(IKeySource keySource)
    {
        _keySource = keySource;
    }

    public byte[] Decrypt(string keyRef, string base64Payload)
    {
        var blob = DecodeBlob(base64Payload);
        var key = ResolveKey(keyRef);
        return DecryptBlob(blob, key);
    }

    public string Encrypt(string keyRef, byte[] plaintext)
    {
        var key = _keySource.GetKey(keyRef);

        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var cipher = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        using (var aes = new AesGcm(key, TagSize))
            aes.Encrypt(nonce, plaintext, cipher, tag);

        return PackBlob(nonce, cipher, tag);
    }

    #region Private Methods

    private static byte[] DecodeBlob(string base64Payload)
    {
        if (string.IsNullOrWhiteSpace(base64Payload))
            throw new PayloadDecryptionException("Empty payload.");

        byte[] blob;
        try
        {
            blob = Convert.FromBase64String(base64Payload.Trim());
        }
        catch (FormatException)
        {
            throw new PayloadDecryptionException("Payload is not valid base64.");
        }

        if (blob.Length < NonceSize + TagSize + 1)
            throw new PayloadDecryptionException("Payload too short.");

        return blob;
    }

    private byte[] ResolveKey(string keyRef)
    {
        try
        {
            return _keySource.GetKey(keyRef);
        }
        catch (Exception)
        {
            throw new PayloadDecryptionException("Key unavailable.");
        }
    }

    private static byte[] DecryptBlob(byte[] blob, byte[] key)
    {
        var nonce = blob.AsSpan(0, NonceSize);
        var tag = blob.AsSpan(blob.Length - TagSize, TagSize);
        var cipher = blob.AsSpan(NonceSize, blob.Length - NonceSize - TagSize);
        var plaintext = new byte[cipher.Length];

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plaintext);
            return plaintext;
        }
        catch (CryptographicException)
        {
            throw new PayloadDecryptionException("Authentication failed.");
        }
    }

    private static string PackBlob(byte[] nonce, byte[] cipher, byte[] tag)
    {
        var blob = new byte[NonceSize + cipher.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, blob, 0, NonceSize);
        Buffer.BlockCopy(cipher, 0, blob, NonceSize, cipher.Length);
        Buffer.BlockCopy(tag, 0, blob, NonceSize + cipher.Length, TagSize);
        return Convert.ToBase64String(blob);
    }

    #endregion
}
