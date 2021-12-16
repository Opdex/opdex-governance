using Stratis.SmartContracts;

public struct VaultProposalWithdrawPledgeLog
{
    [Index] public ulong ProposalId;
    [Index] public Address Pledger;
    public ulong WithdrawAmount;
    public ulong PledgerAmount;
    public ulong ProposalPledgeAmount;
    public bool PledgeWithdrawn;
}
