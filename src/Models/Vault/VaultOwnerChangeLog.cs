using Stratis.SmartContracts;

public struct VaultOwnerChangeLog
{
    [Index] public Address From;
    [Index] public Address To;
}