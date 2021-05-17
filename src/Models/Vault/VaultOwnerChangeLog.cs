using Stratis.SmartContracts;

public struct ChangeVaultOwnerLog
{
    [Index] public Address From;
    [Index] public Address To;
}