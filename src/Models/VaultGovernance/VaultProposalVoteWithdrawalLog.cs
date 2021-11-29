using Stratis.SmartContracts;

public struct VaultProposalVoteWithdrawalLog
{
    [Index] public Address Voter;
    [Index] public UInt256 ProposalId;
    public ulong Amount;
    public ulong RemainingAmount;
    public ulong TotalProposalYesWeight;
    public ulong TotalProposalNoWeight;
}
