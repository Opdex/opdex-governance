using Stratis.SmartContracts;

public struct DistributionLog
{
    public UInt256 VaultAmount;
    public UInt256 MiningAmount;
    [Index] public uint PeriodIndex;
}