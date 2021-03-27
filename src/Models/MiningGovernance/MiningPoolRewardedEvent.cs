using Stratis.SmartContracts;

public struct MiningPoolRewardedEvent
{
    [Index] public Address StakingPool;
    [Index] public Address MiningPool;
    public UInt256 Amount;
}