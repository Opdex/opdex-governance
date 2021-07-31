using Stratis.SmartContracts;

public struct DistributionLog
{
    [Index] public uint PeriodIndex;
    public UInt256 VaultAmount;
    public UInt256 MiningAmount;
    public UInt256 TotalSupply;
}