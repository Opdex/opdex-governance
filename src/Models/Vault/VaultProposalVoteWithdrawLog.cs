using Stratis.SmartContracts;

public struct VaultProposalVoteWithdrawLog
{
    [Index] public Address Voter;
    [Index] public UInt256 ProposalId;
    public ulong WithdrawAmount;
    public ulong VoterAmount;
    public ulong ProposalYesAmount;
    public ulong ProposalNoAmount;
    public bool VoteWithdrawn;
}
