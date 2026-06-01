namespace MongoApi.Infrastructure;

public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message) { }
}
