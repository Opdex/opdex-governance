using Stratis.SmartContracts;

/// <summary>
/// Log emitted when a vote after a successful proposal vote.
/// </summary>
public struct VaultProposalVoteLog
{
    /// <summary>
    /// The Id number of the proposal.
    /// </summary>
    [Index] public UInt256 ProposalId;

    /// <summary>
    /// The wallet address of the voter.
    /// </summary>
    [Index] public Address Voter;

    /// <summary>
    /// Flag determining if the vote was in favor of the proposal or not.
    /// </summary>
    public bool InFavor;

    /// <summary>
    /// The weight added to a new or existing vote.
    /// </summary>
    public ulong Weight;

    /// <summary>
    /// The total weight of the vote.
    /// </summary>
    public ulong TotalVoterWeight;

    public ulong TotalProposalYesWeight;
    public ulong TotalProposalNoWeight;
}
