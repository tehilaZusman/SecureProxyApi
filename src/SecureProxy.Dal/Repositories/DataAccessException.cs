namespace SecureProxy.Dal.Repositories;

public sealed class DataAccessException : Exception
{
    public DataAccessException(string message, Exception inner) : base(message, inner) { }
}
