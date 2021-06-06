using Stratis.SmartContracts;

public interface IOpdexMiningGovernance
{
    /// <summary>
    /// The address of the token being mined.
    /// </summary>
    Address MinedToken { get; }

    /// <summary>
    /// The end block of the current nomination period.
    /// </summary>
    ulong NominationPeriodEnd { get; }

    /// <summary>
    /// The amount of times mining pools have been funded, resets at 48.
    /// </summary>
    uint MiningPoolsFunded { get; }

    /// <summary>
    /// The amount of tokens to reward to a mining pool contract.
    /// </summary>
    UInt256 MiningPoolReward { get; }

    /// <summary>
    /// Contract reentrancy locked status.
    /// </summary>
    bool Locked { get; }

    /// <summary>
    /// Flag describing if tokens have been distributed to this contract and that this contract has been notified.
    /// Used to allow or deny calculation of the next period's mining rewards per mining pool.
    /// </summary>
    bool Notified { get; }

    /// <summary>
    /// The top 4 staking pool nominations by weight.
    /// </summary>
    Nomination[] Nominations { get; }

    /// <summary>
    /// The number of blocks of each nomination/mining period.
    /// </summary>
    ulong MiningDuration { get; }

    /// <summary>
    /// Retrieve a mining pool address by the liquidity pool token address.
    /// </summary>
    /// <param name="stakingToken">The address of the liquidity pool and its liquidity pool token.</param>
    /// <returns>Address of the mining pool associated.</returns>
    Address GetMiningPool(Address stakingToken);

    /// <summary>
    /// Nominate a liquidity pool for liquidity mining based on staking weight.
    /// Only the <see cref="MinedToken"/> contract can make this call.
    /// </summary>
    /// <param name="stakingToken">The address of the liquidity pool and it's token.</param>
    /// <param name="weight">The current balance of staked weight in the liquidity pool.</param>
    void NominateLiquidityPool(Address stakingToken, UInt256 weight);

    /// <summary>
    /// Loops nominations, rewards and notifies mining pool contracts of funding.
    /// </summary>
    void RewardMiningPools();

    /// <summary>
    /// Fallback for <see cref="RewardMiningPools"/> if gas costs become too high.
    /// Rewards and notifies the next liquidity mining pool contract of funding.
    /// </summary>
    void RewardMiningPool();

    /// <summary>
    /// Hook to notify this governance contract that funding from the token has been sent.
    /// </summary>
    /// <param name="firstNomination">The first nomination's liquidity pool address, only used for genesis distribution.</param>
    /// <param name="secondNomination">The second nomination's liquidity pool address, only used for genesis distribution.</param>
    /// <param name="thirdNomination">The third nomination's liquidity pool address, only used for genesis distribution.</param>
    /// <param name="fourthNomination">The fourth nomination's liquidity pool address, only used for genesis distribution.</param>
    void NotifyDistribution(Address firstNomination, Address secondNomination, Address thirdNomination, Address fourthNomination);
}