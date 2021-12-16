using Stratis.SmartContracts;

public struct CreateVaultProposalLog
{
    [Index] public ulong ProposalId;
    [Index] public Address Wallet;
    public UInt256 Amount;
    public byte Type;
    public byte Status;
    public ulong Expiration;
    public string Description;
}
