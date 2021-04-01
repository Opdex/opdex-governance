using Stratis.SmartContracts;

public interface IOpdexMiningGovernance
{
    /// <summary>
    /// The address of the token being mined (rewarded).
    /// </summary>
    Address MinedToken { get; }
        
    /// <summary>
    /// The end block of the current nomination period.
    /// </summary>
    ulong NominationPeriodEnd { get; }
        
    /// <summary>
    /// The index of the last rewarded bucket, to track how many liquidity
    /// mining contracts have been deployed and funded on a per year basis.
    /// </summary>
    uint MiningPoolsFunded { get; }
        
    /// <summary>
    /// The amount of mined tokens to fund each liquidity mining contract.
    /// </summary>
    UInt256 MiningPoolReward { get; }
        
    /// <summary>
    /// Contract reentrancy locked status.
    /// </summary>
    bool Locked { get; }
    
    /// <summary>
    /// Flag describing if tokens have been distributed to this contract and is ready for calculating the yearly
    /// mining amounts. Resets back to false after yearly calculations.
    /// </summary>
    bool Distributed { get; }
        
    /// <summary>
    /// Top 4 staking pool nominations by staking weight.
    /// </summary>
    Nomination[] Nominations { get; }

    /// <summary>
    /// Retrieve the mining pool address by the liquidity pool token address. 
    /// </summary>
    /// <param name="stakingToken">The address of the liquidity pool and it's liquidity pool token.</param>
    /// <returns>Address of the mining pool associated.</returns>
    Address GetMiningPool(Address stakingToken);
        
    /// <summary>
    /// Nominate a liquidity pool for liquidity mining based on staking weight.
    /// Only the MinedToken contract can make this call.
    /// </summary>
    /// <param name="stakingToken">The address of the liquidity pool's and it's token.</param>
    /// <param name="weight">The current balance of staked weight in the liquidity pool.</param>
    void NominateLiquidityPool(Address stakingToken, UInt256 weight);
        
    /// <summary>
    /// Loops nominations, funds and notifies liquidity mining pool contracts of funding.
    /// </summary>
    void NotifyMiningPools();
        
    /// <summary>
    /// Fallback for <see cref="NotifyMiningPools"/> if gas costs become too high.
    /// Funds and notifies liquidity mining pool contracts of funding.
    /// </summary>
    void NotifyMiningPool();

    /// <summary>
    /// Hook to notify this governance contract that funding from the token has been sent.
    /// </summary>
    /// <param name="data">Genesis liquidity pool addresses to set for mining. Not used after initial token distribution.</param>
    void NotifyDistribution(byte[] data);
}