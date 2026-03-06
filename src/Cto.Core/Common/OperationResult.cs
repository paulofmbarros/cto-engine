namespace Cto.Core.Common;

public sealed class OperationResult
{
    public bool Success { get; private set; } = true;
    public List<string> Messages { get; } = [];

    public static OperationResult Ok(params string[] messages)
    {
        var result = new OperationResult();
        result.Messages.AddRange(messages);
        return result;
    }

    public static OperationResult Ok(IEnumerable<string> messages)
    {
        var result = new OperationResult();
        result.Messages.AddRange(messages);
        return result;
    }

    public static OperationResult Fail(params string[] messages)
    {
        var result = new OperationResult { Success = false };
        result.Messages.AddRange(messages);
        return result;
    }

    public static OperationResult Fail(IEnumerable<string> messages)
    {
        var result = new OperationResult { Success = false };
        result.Messages.AddRange(messages);
        return result;
    }

    public void AddMessage(string message) => Messages.Add(message);

    public void AddFailure(string message)
    {
        Success = false;
        Messages.Add(message);
    }
}
