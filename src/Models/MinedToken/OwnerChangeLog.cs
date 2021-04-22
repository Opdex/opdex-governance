using Stratis.SmartContracts;

public struct OwnerChangeLog
{
    [Index] public Address From;
    [Index] public Address To;
}