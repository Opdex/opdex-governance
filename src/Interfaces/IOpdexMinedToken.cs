using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

public interface IOpdexMinedToken : IStandardToken256
{
    /// <summary>
    /// The address of the contract creator.
    /// </summary>
    Address Creator { get; }

    /// <summary>
    /// The address of the mining governance contract.
    /// </summary>
    Address MiningGovernance { get; }

    /// <summary>
    /// The address of the vault contract.
    /// </summary>
    Address Vault { get; }

    /// <summary>
    /// The scheduled amounts to mint to the vault contract.
    /// </summary>
    UInt256[] VaultSchedule { get; }

    /// <summary>
    /// The scheduled amounts to mint to the mining governance contract.
    /// </summary>
    UInt256[] MiningSchedule { get; }

    /// <summary>
    /// The initial block the token was first distributed at.
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
    /// Mints and distributes tokens according to the vault and mining period schedules.
    /// </summary>
    void Distribute();

    /// <summary>
    /// Mints and distributions genesis tokens while also nominating the first four liquidity pools to have liquidity mining enabled.
    /// </summary>
    /// <param name="firstNomination">The first nomination's liquidity pool address.</param>
    /// <param name="secondNomination">The second nomination's liquidity pool address.</param>
    /// <param name="thirdNomination">The third nomination's liquidity pool address.</param>
    /// <param name="fourthNomination">The fourth nomination's liquidity pool address.</param>
    void DistributeGenesis(Address firstNomination, Address secondNomination, Address thirdNomination, Address fourthNomination);

    /// <summary>
    /// Nominates a liquidity pool by its staking weight for liquidity mining. The caller must be a smart contract and must have a balance.
    /// </summary>
    void NominateLiquidityPool();
}