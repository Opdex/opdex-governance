# Opdex Token Contract

Standard SRC20 token contract with add mining and governance abilities. Distributes tokens based on a schedule to an owner and a mining governance 
smart contract, created when deploying this token contract. 

The token is used to stake in Opdex liquidity pools to earn a portion of transaction fees through swaps, offsetting costs such as gas, slippage,
impermanent loss and protocol transaction fees.

After a user stakes or withdraws staked OPDX tokens, Opdex liquidity pools automatically report to this contract.
The pool's OPDX balance (staking weight) is checked and reported to the mining governance contract.

The mining governance contract holds the yearly distributed balance to be mined and OPDX tokens are released to individual mining pool
contracts for liquidity mining based on nominations. Each period, the top 4 liquidity pools with the highest staking weight are rewarded
OPDX to mine.


## Distribute

```c#
/// <summary>
/// Mints and distributes tokens according to the owner and mining period schedules.
/// </summary>
/// <param name="data">
/// Serialized Address[] of initial staking pools to be granted funds for liquidity mining.
/// Not used after initial token distribution.
/// </param>
void Distribute(byte[] data);
```

## Set Owner

```c#
/// <summary>
/// Updates the current owner of the token to a new owner address. Only the current owner can set a new owner.
/// </summary>
/// <param name="owner">Address of the new owner to set.</param>
void SetOwner(Address owner);
```

## Nominate Liquidity Pool

```c#
/// <summary>
/// Nominates a liquidity pool by its staking weight for liquidity mining. The caller must be a smart contract
/// and must have an OPDX Token balance.
/// </summary>
void NominateLiquidityPool();
```