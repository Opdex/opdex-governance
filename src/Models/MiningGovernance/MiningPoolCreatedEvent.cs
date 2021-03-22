using Stratis.SmartContracts;

public struct MiningPoolCreatedEvent
{
    [Index] public Address StakingPool;
    [Index] public Address MiningPool;
}