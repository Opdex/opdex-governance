using Stratis.SmartContracts;

public struct ExitMiningPoolLog
{
    [Index] public Address Miner;
    public UInt256 Amount;
}