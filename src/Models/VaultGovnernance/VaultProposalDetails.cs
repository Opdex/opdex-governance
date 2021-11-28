using Stratis.SmartContracts;

public struct VaultProposalDetails
{
    public UInt256 Id;
    public UInt256 Amount;
    public Address Holder;
    public Address CreatedBy;
    public string Proposal;
    public bool Completed;
    public string Type;
    public ulong YesWeight;
    public ulong NoWeight;
    public ulong EndBlock;
}