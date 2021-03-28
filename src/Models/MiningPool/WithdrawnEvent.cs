using Stratis.SmartContracts;

public struct WithdrawnEvent
{
    [Index] public Address User;
    public UInt256 Amount;
}