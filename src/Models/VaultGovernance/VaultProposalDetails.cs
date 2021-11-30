using Stratis.SmartContracts;

public struct VaultProposalDetails
{
    public UInt256 Amount;
    public Address Recipient;
    public VaultProposalType Type;
    public VaultProposalStatus Status;
    public ulong Expiration;
    public string Description;
    public ulong YesAmount;
    public ulong NoAmount;
    public ulong PledgeAmount;
}
