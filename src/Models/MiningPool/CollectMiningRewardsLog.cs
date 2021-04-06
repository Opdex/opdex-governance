using Stratis.SmartContracts;

public struct CollectMiningRewardsLog
{
    [Index] public Address Miner;
    public UInt256 Amount;
}