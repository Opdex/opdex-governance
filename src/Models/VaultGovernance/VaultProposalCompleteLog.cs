using Stratis.SmartContracts;

public struct VaultProposalCompleteLog
{
    [Index] public UInt256 ProposalId;
    public bool Approved;
}
