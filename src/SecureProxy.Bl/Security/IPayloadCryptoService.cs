namespace SecureProxy.Bl.Security;

public sealed class PayloadDecryptionException : Exception
{
    public PayloadDecryptionException(string message) : base(message) { }
}

public interface IPayloadCryptoService
{
    byte[] Decrypt(string keyRef, string base64Payload);

    string Encrypt(string keyRef, byte[] plaintext);
}
