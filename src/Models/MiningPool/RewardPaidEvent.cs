using Stratis.SmartContracts;

public struct RewardPaidEvent
{
    [Index] public Address User;
    public UInt256 Reward;
}