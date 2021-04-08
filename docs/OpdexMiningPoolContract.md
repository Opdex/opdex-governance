# Opdex Mining Pool Contract

Mining pool smart contract for mining new OPDX tokens. Each mining pool contract is tied to an individual Opdex liquidity
pool. 

Based on staking weight, the mining governance contract distributes tokens to be mined to mining pool contracts. Users provide 
liquidity to the Opdex liquidity pool, in return receiving liquidity pool tokens, and stake those liquidity pool tokens in their
associated mining pool contract to earn OPDX tokens.

## Get Reward Per Token Paid

```c#
/// <summary>
/// Retrieves the last calculated reward per token for the provided address.
/// </summary>
/// <param name="address">The miners address.</param>
/// <returns>The last calculated amount of tokens earned per liquidity pool token used for mining.</returns>
UInt256 GetRewardPerTokenPaid(Address address);
```

## Get Reward

```c#
/// <summary>
/// Retrieves the last calculated reward amount from state for a provided address.
/// </summary>
/// <param name="address">The address of the address to check the rewards for.</param>
/// <returns>The number of earned tokens from mining.</returns>
UInt256 GetReward(Address address);
```

## Get Balance

```c#
/// <summary>
/// Returns the balance of liquidity pool tokens used for mining from a provided address.
/// </summary>
/// <param name="address">The address of the wallet to check the balance of.</param>
/// <returns>The number of liquidity pool tokens held in the mining pool for mining.</returns>
UInt256 GetBalance(Address address);
```

## Latest Block Applicable
```c#
/// <summary>
/// Returns either the current block number or the last block of the mining period, whichever is less.
/// </summary>
/// <returns>The latest applicable block number.</returns>
ulong LatestBlockApplicable();
```

## Get Reward For Duration

```c#
/// <summary>
/// Calculates the total tokens being distributed for the current mining period.
/// </summary>
/// <returns>The number of tokens distributed throughout the entire current mining period.</returns>
UInt256 GetRewardForDuration();
```

## Get Reward Per Token Paid

```c#
/// <summary>
/// Calculates and returns the expected earnings per liquidity pool token used to mine based on the
/// current state including remaining mining period, total tokens mining, and the reward rate.
/// </summary>
/// <returns>Amount of earnings per token used to mine based on the current state of the pool.</returns>
UInt256 GetRewardPerToken();
```
  
## Earned

```c#
/// <summary>
/// Calculates and returns the amount of mined tokens earned by the miner.
/// </summary>
/// <param name="address">The wallet address toa check earned rewards for.</param>
/// <returns>Amount of tokens earned through mining.</returns>
UInt256 Earned(Address address);
```  

## Mine

```c#
/// <summary>
/// Use liquidity pool tokens to mine and earn rewarded tokens.
/// </summary>
/// <param name="amount">
/// The amount of liquidity pool tokens to mine with. Calling this method requires an approved
/// allowance of this amount.
/// </param>
void Mine(UInt256 amount);
```

## Collect

```c#
/// <summary>
/// Collects and transfers earned rewards to miner.
/// </summary>
void Collect();
```
    

    
## Exit

```c#
/// <summary>
/// Withdraws all staked liquidity pool tokens and collects mined rewards.
/// </summary>
void Exit();
```
    
## Notify Reward Amount

```c#
/// <summary>
/// Hook used to notify this mining pool of rewarded funding. Sets reward rates and mining periods.
/// </summary>
/// <param name="reward">The amount of tokens rewarded to the mining pool for mining.</param>
void NotifyRewardAmount(UInt256 reward);
```

___

Ported and adjusted based on https://github.com/Synthetixio/synthetix/tree/v2.27.2/
    