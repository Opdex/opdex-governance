using Stratis.SmartContracts;

public struct RewardMiningPoolLog
{
    [Index] public Address StakingPool;
    [Index] public Address MiningPool;
    public UInt256 Amount;
}