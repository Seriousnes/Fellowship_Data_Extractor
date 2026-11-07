namespace Fellowship.SDK.Models;

/// <summary>
/// An effect's duration as a share of an attribute. Cooldown Recovery Bonus lasts 10% of
/// <c>WeaponAbilityCooldown</c> at rank 1.
/// </summary>
/// <param name="Attribute">The captured attribute.</param>
/// <param name="Coefficients">The coefficient at each rank, from rank 1. A row without ranks has one.</param>
public sealed record DurationScaling(string Attribute, IReadOnlyList<double> Coefficients);
