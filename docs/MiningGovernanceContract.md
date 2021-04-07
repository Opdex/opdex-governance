# Opdex Mining Governance Contract

A simple governance smart contract responsible for holding tokens to be distributed, distributing held tokens based on a schedule,
and choosing which mining pools to distribute to based on a nomination by staking weight.

## Get Mining Pool

```c#
/// <summary>
/// Retrieve the mining pool address by the liquidity pool token address. 
/// </summary>
/// <param name="stakingToken">The address of the liquidity pool and it's liquidity pool token.</param>
/// <returns>Address of the mining pool associated.</returns>
Address GetMiningPool(Address stakingToken);
```

## Nominate Liquidity Pool
```c#
/// <summary>
/// Nominate a liquidity pool for liquidity mining based on staking weight.
/// Only the MinedToken contract can make this call.
/// </summary>
/// <param name="stakingToken">The address of the liquidity pool's and it's token.</param>
/// <param name="weight">The current balance of staked weight in the liquidity pool.</param>
void NominateLiquidityPool(Address stakingToken, UInt256 weight);
```

## Notify Mining Pools

```c#
/// <summary>
/// Loops nominations, funds and notifies liquidity mining pool contracts of funding.
/// </summary>
void NotifyMiningPools();
```

## Notify Mining Pool

```c#
/// <summary>
/// Fallback for <see cref="NotifyMiningPools"/> if gas costs become too high.
/// Funds and notifies liquidity mining pool contracts of funding.
/// </summary>
void NotifyMiningPool();
```

## Notify Distribution

```c#
/// <summary>
/// Hook to notify this governance contract that funding from the token has been sent.
/// </summary>
/// <param name="data">Genesis liquidity pool addresses to set for mining. Not used after initial token distribution.</param>
void NotifyDistribution(byte[] data);
```