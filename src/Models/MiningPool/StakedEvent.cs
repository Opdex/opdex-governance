using Stratis.SmartContracts;

public struct StakedEvent
{
    [Index] public Address To;
    public UInt256 Amount;  
}