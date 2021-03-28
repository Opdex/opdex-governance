using Stratis.SmartContracts;

public struct StakedEvent
{
    [Index] public Address User;
    public UInt256 Amount;  
}