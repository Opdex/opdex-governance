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
    /// The vault's governance address.
    /// </summary>
    /// <remarks>
    /// The vault governance's privileges include the ability to create and revoke vault certificates.
    /// </remarks>
    Address Governance { get; }

    /// <summary>
    /// The total supply of tokens available to be locked in certificates.
    /// </summary>
    UInt256 TotalSupply { get; }

    /// <summary>
    /// The number of blocks required for certificates to be locked before being redeemed.
    /// </summary>
    ulong VestingDuration { get; }

    /// <summary>
    /// Retrieves a certificate a given address holds.
    /// </summary>
    /// <param name="wallet">The wallet address to check a certificate for.</param>
    /// <returns>A <see cref="VaultCertificate"/> that the sending address has been issued.</returns>
    VaultCertificate GetCertificate(Address wallet);

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
    /// Redeems a vested certificate the sender owns and transfers the claimed tokens.
    /// </summary>
    void RedeemCertificate();

    /// <summary>
    /// Allows the vault governance to revoke a non-vested certificate by deducting from the certificate amount based on how many blocks
    /// the certificate was vested for. Revoked certificates still hold a balance and still hold the same vested block.
    /// </summary>
    /// <param name="wallet">The wallet address to revoke a non-vested certificate for.</param>
    void RevokeCertificate(Address wallet);
}