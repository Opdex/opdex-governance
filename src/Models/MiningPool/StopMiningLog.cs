using Stratis.SmartContracts;

public struct StopMiningLog
{
    [Index] public Address Miner;
    public UInt256 Amount;
}