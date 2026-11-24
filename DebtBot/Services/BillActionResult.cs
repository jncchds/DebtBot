namespace DebtBot.Services;

public enum BillActionStatus
{
    Ok,
    /// <summary>The bill doesn't exist or the caller can't see it.</summary>
    NotFound,
    /// <summary>The caller can see the bill but isn't its creator or an admin.</summary>
    Forbidden,
    /// <summary>The request or the bill's state doesn't allow the action; see <see cref="BillActionResult{T}.Errors"/>.</summary>
    Invalid,
    /// <summary>The bill changed state meanwhile (e.g. it was finalized), so the action no longer applies.</summary>
    Conflict
}

public record BillActionResult<T>(BillActionStatus Status, T? Value, List<string> Errors)
{
    public static BillActionResult<T> Ok(T value) => new(BillActionStatus.Ok, value, []);
    public static BillActionResult<T> NotFound() => new(BillActionStatus.NotFound, default, []);
    public static BillActionResult<T> Forbidden() => new(BillActionStatus.Forbidden, default, []);
    public static BillActionResult<T> Invalid(List<string> errors) => new(BillActionStatus.Invalid, default, errors);
    public static BillActionResult<T> Invalid(string error) => new(BillActionStatus.Invalid, default, [error]);
    public static BillActionResult<T> Conflict(string error) => new(BillActionStatus.Conflict, default, [error]);

    public BillActionResult<TOther> As<TOther>() => new(Status, default, Errors);
}
