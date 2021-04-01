using Stratis.SmartContracts;

public struct NominationEvent
{
    [Index] public Address StakingPool;
    [Index] public Address MiningPool;
    public UInt256 Weight;
}