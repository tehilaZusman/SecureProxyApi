namespace SecureProxy.Bl.Security;

public interface IKeySource
{
    byte[] GetKey(string keyRef);
}
