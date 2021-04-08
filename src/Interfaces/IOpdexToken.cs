using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

public interface IOpdexToken : IStandardToken256
{
    /// <summary>
    /// The address of the owner.
    /// </summary>
    Address Owner { get; }
    
    /// <summary>
    /// The address of the mining governance contract.
    /// </summary>
    Address MiningGovernance { get; }
    
    /// <summary>
    /// The scheduled amounts to mint to the owner.
    /// </summary>
    UInt256[] OwnerSchedule { get; }
    
    /// <summary>
    /// The scheduled amounts to mint for mining.
    /// </summary>
    UInt256[] MiningSchedule { get; }
    
    /// <summary>
    /// The initial block the token can start being distributed.
    /// </summary>
    ulong Genesis { get; }
    
    /// <summary>
    /// The number of periods that have been distributed.
    /// </summary>
    uint PeriodIndex { get; }
    
    /// <summary>
    /// The number of blocks between token distribution periods.
    /// </summary>
    ulong PeriodDuration { get; }
    
    /// <summary>
    /// The token symbol.
    /// </summary>
    string Symbol { get; }
    
    /// <summary>
    /// The token name.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Mints and distributes tokens according to the owner and mining period schedules.
    /// </summary>
    /// <param name="data">
    /// Serialized Address[] of initial staking pools to be granted funds for liquidity mining.
    /// Not used after initial token distribution.
    /// </param>
    void Distribute(byte[] data);

    /// <summary>
    /// Updates the current owner of the token to a new owner address. Only the current owner can set a new owner.
    /// </summary>
    /// <param name="owner">Address of the new owner to set.</param>
    void SetOwner(Address owner);

    /// <summary>
    /// Nominates a liquidity pool by its staking weight for liquidity mining. The caller must be a smart contract
    /// and must have an OPDX Token balance.
    /// </summary>
    void NominateLiquidityPool();
}