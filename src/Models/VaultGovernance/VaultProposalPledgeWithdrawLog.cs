using Stratis.SmartContracts;

public struct VaultProposalPledgeWithdrawLog
{
    [Index] public Address Voter;
    [Index] public UInt256 ProposalId;
    public ulong WithdrawAmount;
    public ulong PledgerAmount;
    public ulong ProposalPledgeAmount;
}
