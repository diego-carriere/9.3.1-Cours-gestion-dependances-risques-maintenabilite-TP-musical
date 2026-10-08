namespace ReveilMusical.Domain.Results;

/// <summary>
/// Catégories d'échecs attendus. Jamais une exception : l'hôte HTTP en fait le mapping vers un
/// code de statut, dans un seul fichier.
/// </summary>
public enum ErrorKind
{
    InvalidRequest,
    UserNotFound,
    UserServiceUnavailable,
    ProviderUnavailable,
    InvalidContact,
    ChannelUnavailable,
    Timeout,
}

public sealed record DomainError(ErrorKind Kind, string Message);

/// <summary>
/// Résultat d'une opération qui peut échouer de façon attendue (utilisateur inconnu, fournisseur
/// ou canal en panne...). Les bugs continuent de lever des exceptions ; ce type ne sert qu'aux
/// échecs qui font partie du comportement normal. Construit via la classe statique non générique
/// <see cref="Result"/> (CA1000).
/// </summary>
public readonly record struct Result<T>
{
    private readonly T? _value;
    private readonly DomainError? _error;

    private Result(bool isSuccess, T? value, DomainError? error)
    {
        IsSuccess = isSuccess;
        _value = value;
        _error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    /// <summary>La valeur de succès. Lève si <see cref="IsSuccess"/> est faux.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value of a failed Result.");

    /// <summary>L'erreur d'échec. Lève si <see cref="IsSuccess"/> est vrai.</summary>
    public DomainError Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Cannot access Error of a successful Result.");

    /// <summary>
    /// Remplace le ToString synthétisé du record, qui lirait <see cref="Value"/> et lèverait sur un
    /// échec : un résultat doit toujours pouvoir être journalisé (Polly le fait à chaque tentative).
    /// </summary>
    public override string ToString() => IsSuccess ? $"Success({_value})" : $"Failure({_error!.Kind}: {_error.Message})";

    internal static Result<T> CreateSuccess(T value) => new(true, value, null);

    internal static Result<T> CreateFailure(DomainError error) => new(false, default, error);
}

/// <summary>Fabriques pour <see cref="Result{T}"/>.</summary>
public static class Result
{
    public static Result<T> Success<T>(T value) => Result<T>.CreateSuccess(value);

    public static Result<T> Failure<T>(DomainError error) => Result<T>.CreateFailure(error);

    public static Result<T> Failure<T>(ErrorKind kind, string message) => Failure<T>(new DomainError(kind, message));
}
