using Stratis.SmartContracts;

public interface IOpdexVault
{
    /// <summary>
    /// The token the vault is responsible for locking.
    /// </summary>
    Address Token { get; }
    
    /// <summary>
    /// The vault owner's address
    /// </summary>
    Address Owner { get; }
    
    /// <summary>
    /// List of certificates a given address holds.
    /// </summary>
    /// <param name="address">The wallet address to check certificates for.</param>
    /// <returns>An array of <see cref="VaultCertificate"/> the address has been issued.</returns>
    VaultCertificate[] GetCertificates(Address address);
    
    /// <summary>
    /// Flag to allow the token to notify the vault of distribution and to assign a new
    /// certificate to the vault owner.
    /// </summary>
    /// <param name="amount">The amount of tokens locked.</param>
    void NotifyDistribution(UInt256 amount);
    
    /// <summary>
    /// Redeems all certificates and transfers the locked tokens to the transaction sender.
    /// </summary>
    void RedeemCertificates();
    
    /// <summary>
    /// Redeems one certificate and transfers tokens to the transaction sender.
    /// </summary>
    void RedeemCertificate();

    /// <summary>
    /// Allows the vault owner to issue new certificates to wallet addresses with a set vesting period.
    /// </summary>
    /// <param name="to">The address to assign the certificate of tokens to.</param>
    /// <param name="amount">The amount of tokens to assign to the certificate.</param>
    /// <param name="vestingPeriod">The number of blocks the tokens will be locked for.</param>
    /// <returns>Success as a boolean value.</returns>
    bool CreateCertificate(Address to, UInt256 amount, ulong vestingPeriod);
    
    /// <summary>
    /// Updates the current owner of the vault to a new owner address. Only the current owner can set a new owner.
    /// </summary>
    /// <remarks>
    /// Existing certificates will be transferred to the new owner, including certificates that are eligible to be redeemed.
    /// </remarks>
    /// <param name="owner">Address of the new owner to set.</param>
    void SetOwner(Address owner);
}