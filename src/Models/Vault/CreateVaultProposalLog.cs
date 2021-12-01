using Stratis.SmartContracts;

public struct CreateVaultProposalLog
{
    public UInt256 Id;
    public UInt256 Amount;
    public Address Wallet;
    public ProposalType Type;
    public ProposalStatus Status;
    public ulong Expiration;
    public string Description;
}
