namespace ReveilMusical.Domain.Model;

/// <summary>Levée quand aucun canal n'a pu réveiller l'utilisateur : le silence n'est jamais muet.</summary>
public sealed record OperatorAlert(UserId UserId, string Summary);
