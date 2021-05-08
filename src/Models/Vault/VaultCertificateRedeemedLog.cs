using Stratis.SmartContracts;

public struct VaultCertificateRedeemedLog
{
    [Index] public Address Wallet;
    public UInt256 Amount;
}