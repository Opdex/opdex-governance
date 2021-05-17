using Stratis.SmartContracts;

public struct RedeemVaultCertificateLog
{
    [Index] public Address Owner;
    public UInt256 Amount;
    public ulong VestedBlock;
}