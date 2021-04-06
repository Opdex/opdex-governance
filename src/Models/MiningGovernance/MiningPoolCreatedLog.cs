using Stratis.SmartContracts;

public struct MiningPoolCreatedLog
{
    [Index] public Address StakingPool;
    [Index] public Address MiningPool;
}