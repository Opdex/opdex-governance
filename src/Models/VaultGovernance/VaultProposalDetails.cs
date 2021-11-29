using Stratis.SmartContracts;

public struct VaultProposalDetails
{
    public UInt256 Amount;
    public Address Holder;
    public string Description;
    public VaultProposalType Type;
    public ulong EndBlock;
    public bool Completed;
    public ulong YesWeight;
    public ulong NoWeight;
}
