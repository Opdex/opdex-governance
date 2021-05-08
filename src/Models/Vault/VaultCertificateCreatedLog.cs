using Stratis.SmartContracts;

public struct VaultCertificateCreatedLog
{
    [Index] public Address Wallet;
    public UInt256 Amount;
    public ulong VestedBlock;
}