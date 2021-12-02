using Stratis.SmartContracts;

public struct VaultProposalWithdrawVoteLog
{
    [Index] public ulong ProposalId;
    [Index] public Address Voter;
    public ulong WithdrawAmount;
    public ulong VoterAmount;
    public ulong ProposalYesAmount;
    public ulong ProposalNoAmount;
    public bool VoteWithdrawn;
}
