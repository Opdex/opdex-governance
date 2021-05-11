using Stratis.SmartContracts;

public struct VaultCertificateUpdatedLog
{
    [Index] public Address Owner;
    public UInt256 Amount;
    public ulong VestedBlock;
}