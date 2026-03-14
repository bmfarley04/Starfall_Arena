/// <summary>
/// Implemented by ship classes that expose a discrete charge resource to abilities.
/// Abilities can cast their <c>player</c> reference to this interface to read or
/// consume charges without depending on a concrete ship class.
/// </summary>
public interface IChargeProvider
{
    /// <summary>Current number of charges available.</summary>
    int CurrentCharges { get; }

    /// <summary>Maximum number of charges this ship can hold.</summary>
    int MaxCharges { get; }

    /// <summary>
    /// Attempt to spend <paramref name="amount"/> charges.
    /// Returns <c>true</c> and deducts the charges if enough are available;
    /// returns <c>false</c> and leaves the count unchanged otherwise.
    /// </summary>
    bool TrySpendCharges(int amount);

    /// <summary>
    /// Award <paramref name="amount"/> charges (clamped to <see cref="MaxCharges"/>).
    /// </summary>
    void GainCharges(int amount);
}
