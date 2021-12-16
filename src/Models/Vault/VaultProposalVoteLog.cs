using Stratis.SmartContracts;

public struct VaultProposalVoteLog
{
    [Index] public ulong ProposalId;
    [Index] public Address Voter;
    public bool InFavor;
    public ulong VoteAmount;
    public ulong VoterAmount;
    public ulong ProposalYesAmount;
    public ulong ProposalNoAmount;
}
