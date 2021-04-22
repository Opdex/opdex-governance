using Stratis.SmartContracts;

public struct StartMiningLog
{
    [Index] public Address Miner;
    public UInt256 Amount;  
}