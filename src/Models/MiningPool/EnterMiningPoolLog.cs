using Stratis.SmartContracts;

public struct EnterMiningPoolLog
{
    [Index] public Address Miner;
    public UInt256 Amount;  
}