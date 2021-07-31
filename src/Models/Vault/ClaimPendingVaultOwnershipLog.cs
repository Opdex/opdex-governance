using Stratis.SmartContracts;

public struct ClaimPendingVaultOwnershipLog
{
    [Index] public Address From;
    [Index] public Address To;
}