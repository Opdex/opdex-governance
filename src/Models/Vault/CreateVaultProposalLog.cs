using Stratis.SmartContracts;

public struct CreateVaultProposalLog
{
    [Index] public UInt256 Id;
    [Index] public Address Wallet;
    public UInt256 Amount;
    public byte Type;
    public byte Status;
    public ulong Expiration;
    public string Description;
}
