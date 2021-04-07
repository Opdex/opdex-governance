# Opdex Mining Pool Contract

## Get Reward Per Token Paid

```c#
/// <summary>
/// Retrieves the last calculated reward per token for the provided address.
/// </summary>
/// <param name="address"></param>
/// <returns></returns>
UInt256 GetRewardPerTokenPaid(Address address);
```

## Get Reward

```c#
/// <summary>
/// Retrieves the current rewarded amount of mined tokens for the provided address from contract state. Will not update
/// without the address calling a publicly available method which computes this amount.
/// </summary>
/// <param name="address">The address of the address to check the rewards for.</param>
/// <returns>The number of earned tokens from mining.</returns>
UInt256 GetReward(Address address);
```

## Get Balance

```c#
/// <summary>
/// Returns the balance of staked tokens used for mining.
/// </summary>
/// <param name="address">The address of the wallet to check the balance of.</param>
/// <returns>The number of liquidity pool tokens held in the mining pool for mining.</returns>
UInt256 GetBalance(Address address);
```

## Latest Block Applicable
```c#
/// <summary>
/// Checks, validates and returns the last applicable block for mining, either the current block number, or the
/// last block of the mining period.
/// </summary>
/// <returns>The last block number eligible for mining.</returns>
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
/// Calculates and returns the expected earnings per token used to mine with based on the contracts
/// current state including remaining mining period, total staked tokens mining, and the reward rate.
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
/// Stakes liquidity pool tokens to mine and earn rewarded tokens.
/// </summary>
/// <param name="amount">The amount of liquidity pool tokens to mine with.</param>
void Mine(UInt256 amount);
```

## Withdraw

```c#
/// <summary>
/// Withdraws liquidity pool tokens, stopping mining.
/// </summary>
/// <param name="amount">The amount of liquidity pool tokens to withdraw.</param>
void Withdraw(UInt256 amount);
```
    
## Collect

```c#
/// <summary>
/// Withdraws earned mining rewards.
/// </summary>
void Collect();
```
    
## ExitMining

```c#
/// <summary>
/// Withdraws staked liquidity pool tokens and collects mined rewards.
/// </summary>
void ExitMining();
```
    
## Notify Reward Amount

```c#
/// <summary>
/// Hook used to notify this mining pool of rewarded funding. Sets reward rates and mining periods.
/// </summary>
/// <param name="reward">The amount of tokens rewarded to the mining pool for mining.</param>
void NotifyRewardAmount(UInt256 reward);
```
    