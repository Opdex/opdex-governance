using Stratis.SmartContracts;

public struct DistributionLog
{
    [Index] public Address OwnerAddress;
    [Index] public Address MiningAddress;
    public UInt256 OwnerAmount;
    public UInt256 MiningAmount;
    public uint PeriodIndex;
}