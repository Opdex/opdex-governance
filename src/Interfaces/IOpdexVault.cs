using Stratis.SmartContracts;

public interface IOpdexVault
{
    /// <summary>
    /// The SRC token the vault is responsible for locking.
    /// </summary>
    Address Token { get; }

    /// <summary>
    /// The total supply of tokens available to be locked in certificates.
    /// </summary>
    UInt256 TotalSupply { get; }

    /// <summary>
    /// The number of blocks required for certificates to be locked before being redeemed.
    /// </summary>
    ulong VestingDuration { get; }

    /// <summary>
    /// The Id of the next proposal to be created.
    /// </summary>
    UInt256 NextProposalId { get; }

    /// <summary>
    /// The number of tokens requested for active create certificate proposals.
    /// </summary>
    UInt256 ProposedAmount { get; }

    /// <summary>
    /// The total, minimum number of CRS tokens required for a proposal to move onto a vote..
    /// </summary>
    ulong PledgeMinimum { get; }

    /// <summary>
    /// The total, minimum number of CRS tokens required for a proposal to pass.
    /// </summary>
    ulong ProposalMinimum { get; }

    /// <summary>
    /// Retrieves a certificate a given address holds.
    /// </summary>
    /// <param name="wallet">The wallet address to check a certificate for.</param>
    /// <returns>A <see cref="VaultCertificate"/> that the sending address has been issued.</returns>
    VaultCertificate GetCertificate(Address wallet);

    /// <summary>
    /// Retrieves proposal details about a specific proposal.
    /// </summary>
    /// <param name="id">The Id number of the requested proposal.</param>
    /// <returns>Proposal details</returns>
    VaultProposalDetails GetProposal(UInt256 id);

    /// <summary>
    /// Retrieve the vote details of a voter for a specific proposal.
    /// </summary>
    /// <param name="proposalId">The Id number of the proposal.</param>
    /// <param name="voter">The voter's address.</param>
    /// <returns>Details of the vote including weight and in favor or not.</returns>
    VaultProposalVote GetProposalVote(UInt256 proposalId, Address voter);

    /// <summary>
    /// Retrieve a proposal pledge by Id and voter address.
    /// </summary>
    /// <param name="proposalId">The Id of the proposal to retrieve the pledge for.</param>
    /// <param name="voter">The address of the voter.</param>
    /// <returns></returns>
    ulong GetProposalPledge(UInt256 proposalId, Address voter);

    /// <summary>
    /// Method to allow the vault token to notify the vault of distribution to update the total supply.
    /// </summary>
    /// <param name="amount">The amount of tokens sent to the vault.</param>
    void NotifyDistribution(UInt256 amount);

    /// <summary>
    /// Redeems a vested certificate the sender owns and transfers the claimed tokens.
    /// </summary>
    void RedeemCertificate();

    /// <summary>
    /// Creates a new proposal for a vault certificate to either be created or revoked.
    /// </summary>
    /// <param name="amount">The amount of tokens for a creation proposal.</param>
    /// <param name="recipient">The receiving address of a proposed vault certificate.</param>
    /// <param name="description">A short description that includes a Github link to the full proposal.</param>
    /// <param name="type">The type of proposal to create, Create - 1 or Revoke - 2.</param>
    /// <returns>The Id of the generated proposal.</returns>
    UInt256 CreateProposal(UInt256 amount, Address recipient, string description, byte type);

    /// <summary>
    /// Pledge for a proposal by locking CRS tokens to help meet the minimum pledge amount to move to an official vote.
    /// </summary>
    /// <param name="proposalId">The Id of the proposal to pledge to.</param>
    void Pledge(UInt256 proposalId);

    /// <summary>
    /// Votes for or against a proposal by temporarily holding CRS in contract as vote weight.
    /// </summary>
    /// <param name="proposalId">The Id number of the proposal being voted on.</param>
    /// <param name="inFavor">True or false indicating if the vote is in favor of the proposal or against it.</param>
    void ProposalVote(UInt256 proposalId, bool inFavor);


    /// <summary>
    /// Withdraws held CRS from proposal vote, removing the vote if still in progress.
    /// </summary>
    /// <param name="proposalId">The Id number of the proposal voted on.</param>
    /// <param name="withdrawAmount">The amount of tokens to withdraw from the proposal vote.</param>
    void ProposalVoteWithdraw(UInt256 proposalId, ulong withdrawAmount);

    /// <summary>
    /// Withdraw CRS tokens from a pledge transaction, in pledge status proposals will have the pledged amount removed.
    /// </summary>
    /// <param name="proposalId">The Id of the proposal to remove a pledge amount from.</param>
    /// <param name="withdrawAmount">The amount to withdraw from a pledge.</param>
    void ProposalPledgeWithdraw(UInt256 proposalId, ulong withdrawAmount);

    /// <summary>
    /// Completes a proposal and executes the resulting command within the Vault contract if passed, creating or revoking a vault certificate.
    /// </summary>
    /// <param name="proposalId">The Id number of the proposal voted on.</param>
    void CompleteProposal(UInt256 proposalId);
}
