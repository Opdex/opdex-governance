using Stratis.SmartContracts;

public struct NominationLog
{
    [Index] public Address StakingPool;
    [Index] public Address MiningPool;
    public UInt256 Weight;
}