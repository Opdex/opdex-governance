using Stratis.SmartContracts;

public interface IOpdexVault
{
    /// <summary>
    /// The genesis block of the vault contract.
    /// </summary>
    ulong Genesis { get; }
    
    /// <summary>
    /// The token the vault is responsible for locking.
    /// </summary>
    Address Token { get; }
    
    /// <summary>
    /// The vault owner's address.
    /// </summary>
    Address Owner { get; }
    
    /// <summary>
    /// The total supply of the tokens held by this contract.
    /// </summary>
    UInt256 TotalSupply { get; }
    
    /// <summary>
    /// List of certificates a given address holds.
    /// </summary>
    /// <param name="wallet">The wallet address to check certificates for.</param>
    /// <returns>An array of <see cref="VaultCertificate"/> the address has been issued.</returns>
    VaultCertificate[] GetCertificates(Address wallet);
    
    /// <summary>
    /// Method to allow the token to notify the vault of distribution and to assign a new certificate to the vault owner.
    /// </summary>
    /// <remarks>
    /// The vault owner can be reassigned to a new address. Any address can hold onto a maximum of 10 certificates.
    /// If the owner has 10 unclaimed certificates, the distributed tokens to the vault contract will be intentionally
    /// burned because they will not have a certificate that represents them.
    /// </remarks>
    /// <param name="amount">The amount of tokens to lock and create a certificate for.</param>
    void NotifyDistribution(UInt256 amount);
    
    /// <summary>
    /// Allows the vault owner to issue new certificates to wallet addresses with a set vesting period. Created
    /// certificates deduct tokens from existing certificates held by the owner.
    /// </summary>
    /// <param name="to">The address to assign the certificate of tokens to.</param>
    /// <param name="amount">The amount of tokens to assign to the certificate.</param>
    /// <returns>Success as a boolean value.</returns>
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
    /// Updates the current owner of the vault to a new owner address. Only the current owner can set a new owner.
    /// </summary>
    /// <param name="owner">Address of the new owner to set.</param>
    void SetOwner(Address owner);
}