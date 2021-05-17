using Stratis.SmartContracts;

public struct CreateVaultCertificateLog
{
    [Index] public Address Owner;
    public UInt256 Amount;
    public ulong VestedBlock;
}