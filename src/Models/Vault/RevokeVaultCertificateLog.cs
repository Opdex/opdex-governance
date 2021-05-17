using Stratis.SmartContracts;

public struct RevokeVaultCertificateLog
{
    [Index] public Address Owner;
    public UInt256 OldAmount;
    public UInt256 NewAmount;
    public ulong VestedBlock;
}