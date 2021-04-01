using Stratis.SmartContracts;

public struct WithdrawnEvent
{
    [Index] public Address To;
    public UInt256 Amount;
}