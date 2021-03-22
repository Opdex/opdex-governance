using Stratis.SmartContracts;

public struct OwnerChangeEvent
{
    [Index] public Address From;
    [Index] public Address To;
}