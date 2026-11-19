namespace TradeDesk.Core.Domain;

public class TradeValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("The trade request is invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

public class TradeStateException(string message) : Exception(message);

public class TradeNotFoundException(int id) : Exception($"Trade {id} was not found.");
