using Stratis.SmartContracts;

public struct CompleteVaultProposalLog
{
    [Index] public ulong ProposalId;
    public bool Approved;
}
