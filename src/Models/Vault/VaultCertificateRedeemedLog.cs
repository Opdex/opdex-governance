using Stratis.SmartContracts;

public struct VaultCertificateRedeemedLog
{
    [Index] public Address Owner;
    public UInt256 Amount;
}