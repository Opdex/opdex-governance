using Stratis.SmartContracts;

public struct SetPendingVaultOwnershipLog
{
    [Index] public Address From;
    [Index] public Address To;
}