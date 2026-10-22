namespace Cheslax.Application.Common;

public sealed record ApiError(string Code, string Message, IReadOnlyDictionary<string, string[]>? Fields = null);

public sealed class CheslaxException : Exception
{
    public CheslaxException(string code, string message, int statusCode = 400) : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }
    public int StatusCode { get; }
}
