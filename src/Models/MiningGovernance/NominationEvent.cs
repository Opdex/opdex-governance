using Stratis.SmartContracts;

public struct NominationEvent
{
    [Index] public Address StakingToken;
    [Index] public Address MiningPool;
    public UInt256 Weight;
}