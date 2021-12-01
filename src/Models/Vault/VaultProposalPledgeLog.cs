using Stratis.SmartContracts;

public struct VaultProposalPledgeLog
{
    [Index] public UInt256 ProposalId;
    [Index] public Address Wallet;
    public ulong PledgeAmount;
    public ulong PledgerAmount;
    public ulong ProposalPledgeAmount;
    public bool PledgeMinimumMet;
}
