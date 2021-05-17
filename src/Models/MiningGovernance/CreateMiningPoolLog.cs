using Stratis.SmartContracts;

public struct CreateMiningPoolLog
{
    [Index] public Address StakingPool;
    [Index] public Address MiningPool;
}