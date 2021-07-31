using Stratis.SmartContracts;

public interface IOpdexVault
{
    /// <summary>
    /// The genesis block at the time it first received tokens.
    /// </summary>
    ulong Genesis { get; }

    /// <summary>
    /// The SRC token the vault is responsible for locking.
    /// </summary>
    Address Token { get; }

    /// <summary>
    /// The vault owner's address.
    /// </summary>
    /// The vault owner's privileges include the ability to create and revoke vault certificates.
    Address Owner { get; }

    /// <summary>
    /// A pending wallet address that has been suggested to take ownership of the contract. This value
    /// acts as a whitelist for access to <see cref="ClaimPendingOwnership"/>.
    /// </summary>
    Address PendingOwner { get; }

    /// <summary>
    /// The total supply of tokens available to be locked in certificates.
    /// </summary>
    UInt256 TotalSupply { get; }

    /// <summary>
    /// The number of blocks required for certificates to be locked before being redeemed.
    /// </summary>
    ulong VestingDuration { get; }

    /// <summary>
    /// Retrieves a list of certificates a given address holds.
    /// </summary>
    /// <param name="wallet">The wallet address to check certificates for.</param>
    /// <returns>An array of <see cref="VaultCertificate"/>'s the address has been issued.</returns>
    VaultCertificate[] GetCertificates(Address wallet);

    /// <summary>
    /// Method to allow the vault token to notify the vault of distribution to update the total supply.
    /// </summary>
    /// <param name="amount">The amount of tokens sent to the vault.</param>
    void NotifyDistribution(UInt256 amount);

    /// <summary>
    /// Allows the vault owner to issue new certificates to wallet addresses.
    /// </summary>
    /// <param name="to">The address to assign the certificate of tokens to.</param>
    /// <param name="amount">The amount of tokens to assign to the certificate.</param>
    void CreateCertificate(Address to, UInt256 amount);

    /// <summary>
    /// Redeems all vested certificates the sender owns and transfers the claimed tokens.
    /// </summary>
    void RedeemCertificates();

    /// <summary>
    /// Allows the vault owner to revoke non-vested certificates by deducting from the certificate amount based on how many blocks
    /// the certificate was vested for. Revoked certificates still hold a balance and still hold the same vested block.
    /// </summary>
    /// <param name="wallet">The wallet address to revoke non-vested certificates for.</param>
    void RevokeCertificates(Address wallet);

    /// <summary>
    /// Public method allowing the current contract owner to whitelist a new pending owner. The newly pending owner
    /// will then call <see cref="ClaimPendingOwnership"/> to accept.
    /// </summary>
    /// <param name="pendingOwner">The address to set as the new pending owner.</param>
    void SetPendingOwnership(Address pendingOwner);

    /// <summary>
    /// Public method to allow the pending new owner to accept ownership replacing the current contract owner.
    /// </summary>
    void ClaimPendingOwnership();
}