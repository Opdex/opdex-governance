using Stratis.SmartContracts;

public struct CompleteVaultProposalLog
{
    [Index] public UInt256 ProposalId;
    public bool Approved;
}
