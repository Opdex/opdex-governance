using Stratis.SmartContracts;

public struct VaultProposalPledgeLog
{
    [Index] public ulong ProposalId;
    [Index] public Address Pledger;
    public ulong PledgeAmount;
    public ulong PledgerAmount;
    public ulong ProposalPledgeAmount;
    public bool TotalPledgeMinimumMet;
}
