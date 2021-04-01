using Stratis.SmartContracts;

public struct RewardPaidEvent
{
    [Index] public Address To;
    public UInt256 Amount;
}