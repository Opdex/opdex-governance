using Stratis.SmartContracts;

public interface IOpdexMiningPool
{
    /// <summary>
    /// The address of the governance contract in charge of distributing tokens to be mined.
    /// </summary>
    Address MiningGovernance { get; }
    
    /// <summary>
    /// The contract address of the liquidity pool token used for mining..
    /// </summary>
    Address StakingToken { get; }
    
    /// <summary>
    /// The contract address of the token being mined.
    /// </summary>
    Address MinedToken { get; }
    
    /// <summary>
    /// The end block of the mining period.
    /// </summary>
    ulong MiningPeriodEndBlock { get; }
    
    /// <summary>
    /// The amount of tokens mined per block.
    /// </summary>
    UInt256 RewardRate { get; }
    
    /// <summary>
    /// The number of blocks mining is scheduled for.
    /// </summary>
    ulong MiningDuration { get; }
    
    /// <summary>
    /// The last block where a transaction occurred causing reward rate calculations.
    /// </summary>
    ulong LastUpdateBlock { get; }
    
    /// <summary>
    /// The amount of earned tokens per liquidity pool token used for mining, based on the pool's current state.
    /// </summary>
    UInt256 RewardPerToken { get; }
    
    /// <summary>
    /// The total supply of liquidity pool tokens currently mining.
    /// </summary>
    UInt256 TotalSupply { get; }
    
    /// <summary>
    /// Contract reentrancy locked status.
    /// </summary>
    bool Locked { get; }

    /// <summary>
    /// Retrieves the last calculated reward per token for the provided address.
    /// </summary>
    /// <param name="address">The miners address.</param>
    /// <returns>The last calculated amount of tokens earned per liquidity pool token used for mining.</returns>
    UInt256 GetRewardPerTokenPaid(Address address);

    /// <summary>
    /// Retrieves the last calculated reward amount from state for a provided address.
    /// </summary>
    /// <param name="address">The address of the address to check the rewards for.</param>
    /// <returns>The number of earned tokens from mining.</returns>
    UInt256 GetReward(Address address);

    /// <summary>
    /// Returns the balance of liquidity pool tokens used for mining from a provided address.
    /// </summary>
    /// <param name="address">The address of the wallet to check the balance of.</param>
    /// <returns>The number of liquidity pool tokens held in the mining pool for mining.</returns>
    UInt256 GetBalance(Address address);

    /// <summary>
    /// Returns either the current block number or the last block of the mining period, whichever is less.
    /// </summary>
    /// <returns>The latest applicable block number.</returns>
    ulong LatestBlockApplicable();

    /// <summary>
    /// Calculates the total tokens being distributed for the current mining period.
    /// </summary>
    /// <returns>The number of tokens distributed throughout the entire current mining period.</returns>
    UInt256 GetRewardForDuration();

    /// <summary>
    /// Calculates and returns the expected earnings per liquidity pool token used to mine based on the
    /// current state including remaining mining period, total tokens mining, and the reward rate.
    /// </summary>
    /// <returns>Amount of earnings per token used to mine based on the current state of the pool.</returns>
    UInt256 GetRewardPerToken();

    /// <summary>
    /// Calculates and returns the amount of mined tokens earned by the miner.
    /// </summary>
    /// <param name="address">The wallet address toa check earned rewards for.</param>
    /// <returns>Amount of tokens earned through mining.</returns>
    UInt256 Earned(Address address);
    
    /// <summary>
    /// Use liquidity pool tokens to mine and earn rewarded tokens.
    /// </summary>
    /// <param name="amount">
    /// The amount of liquidity pool tokens to mine with. Calling this method requires an approved
    /// allowance of this amount.
    /// </param>
    void Mine(UInt256 amount);
    
    /// <summary>
    /// Collects and transfers earned rewards to miner.
    /// </summary>
    void Collect();
    
    /// <summary>
    /// Withdraws all staked liquidity pool tokens and collects mined rewards.
    /// </summary>
    void Exit();
    
    /// <summary>
    /// Hook used to notify this mining pool of rewarded funding. Sets reward rates and mining periods.
    /// </summary>
    /// <param name="reward">The amount of tokens rewarded to the mining pool for mining.</param>
    void NotifyRewardAmount(UInt256 reward);
}