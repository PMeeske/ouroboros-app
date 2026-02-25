namespace Ouroboros.Application.Personality;

/// <summary>
/// Second-order conditioning - associations between conditioned stimuli.
/// Allows for complex chains of learned associations.
/// </summary>
public sealed record SecondOrderConditioning(
    string Id,
    ConditionedAssociation PrimaryAssociation,
    ConditionedAssociation SecondaryAssociation,
    double ChainStrength,        // Strength of the S1 -> S2 -> R chain
    int ChainDepth)              // How many levels of conditioning
{
    /// <summary>Creates a second-order conditioning chain.</summary>
    public static SecondOrderConditioning Create(
        ConditionedAssociation primary,
        ConditionedAssociation secondary) => new(
        Id: Guid.NewGuid().ToString(),
        PrimaryAssociation: primary,
        SecondaryAssociation: secondary,
        ChainStrength: primary.AssociationStrength * secondary.AssociationStrength,
        ChainDepth: 2);
}