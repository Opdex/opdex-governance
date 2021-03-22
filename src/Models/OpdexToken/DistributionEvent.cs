using Stratis.SmartContracts;

public struct DistributionEvent
{
    [Index] public Address OwnerAddress;
    [Index] public Address MiningAddress;
    public UInt256 OwnerAmount;
    public UInt256 MiningAmount;
    public uint YearIndex;
}