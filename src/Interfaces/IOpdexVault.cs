using Stratis.SmartContracts;

public interface IOpdexVault
{
    /// <summary>
    /// The SRC token the vault is responsible for locking.
    /// </summary>
    Address Token { get; }

    /// <summary>
    /// The total supply of tokens available to be assigned to certificates.
    /// </summary>
    UInt256 TotalSupply { get; }

    /// <summary>
    /// The number of blocks required for certificates to be locked before being redeemed.
    /// </summary>
    ulong VestingDuration { get; }

    /// <summary>
    /// The Id of the next proposal to be created.
    /// </summary>
    ulong NextProposalId { get; }

    /// <summary>
    /// The total number of tokens requested for active create certificate proposals.
    /// </summary>
    UInt256 TotalProposedAmount { get; }

    /// <summary>
    /// The total, minimum number of CRS tokens required for a proposal to move onto a vote.
    /// </summary>
    ulong PledgeMinimum { get; }

    /// <summary>
    /// The total, minimum number of CRS tokens required for a proposal to pass.
    /// </summary>
    ulong ProposalMinimum { get; }

    /// <summary>
    /// Retrieves a certificate a given address is assigned.
    /// </summary>
    /// <param name="wallet">The wallet address to check for a certificate.</param>
    /// <returns>A certificate issued to the provided address.</returns>
    Certificate GetCertificate(Address wallet);

    /// <summary>
    /// Retrieves the details about a proposal.
    /// </summary>
    /// <param name="proposalId">The Id number of the requested proposal.</param>
    /// <returns>Details of the proposal.</returns>
    ProposalDetails GetProposal(ulong proposalId);

    /// <summary>
    /// Retrieve the vote details of a voter for a specific proposal.
    /// </summary>
    /// <param name="proposalId">The Id number of the proposal.</param>
    /// <param name="voter">The voter's address.</param>
    /// <returns>Proposal vote details of the how an address voted.</returns>
    ProposalVote GetProposalVote(ulong proposalId, Address voter);

    /// <summary>
    /// Retrieve the amount of tokens pledged by an address for a proposal.
    /// </summary>
    /// <param name="proposalId">The Id of the proposal to retrieve the pledge of.</param>
    /// <param name="pledger">The address of the pledger.</param>
    /// <returns>The number of CRS tokens pledged to the proposal by the pledger.</returns>
    ulong GetProposalPledge(ulong proposalId, Address pledger);

    /// <summary>
    /// Retrieves an Id of a certificate proposal that the specified recipient has open, limiting to one
    /// proposal per recipient for certificate based proposals at a time.
    /// </summary>
    /// <param name="recipient">The address of the certificate recipient.</param>
    /// <returns>The Id of a proposal that the recipient has open, 0 if no proposals are open.</returns>
    ulong GetCertificateProposalIdByRecipient(Address recipient);

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
    /// Creates a new proposal proposing a certificate be created.
    /// </summary>
    /// <param name="amount">The amount of tokens proposed the recipient's certificate be assigned.</param>
    /// <param name="recipient">The recipient address to assign a certificate to if approved.</param>
    /// <param name="description">A description of the proposal, limited to 200 characters, preferably a link.</param>
    /// <returns>The Id of the generated proposal.</returns>
    ulong CreateNewCertificateProposal(UInt256 amount, Address recipient, string description);

    /// <summary>
    /// Creates a new proposal proposing a certificate be revoked.
    /// </summary>
    /// <param name="recipient">The recipient of an existing certificate to have revoked.</param>
    /// <param name="description">A description of the proposal, limited to 200 characters, preferably a link.</param>
    /// <returns>The Id of the generated proposal.</returns>
    ulong CreateRevokeCertificateProposal(Address recipient, string description);

    /// <summary>
    /// Creates a new proposal proposing a new pledge minimum amount of tokens have pledged to move onto a vote.
    /// </summary>
    /// <param name="amount">The proposed new minimum pledge amount.</param>
    /// <param name="description">A description of the proposal, limited to 200 characters, preferably a link.</param>
    /// <returns>The Id of the generated proposal.</returns>
    ulong CreatePledgeMinimumProposal(UInt256 amount, string description);

    /// <summary>
    /// Creates a new proposal proposing a new proposal minimum amount of tokens have voted to be considered for approval.
    /// </summary>
    /// <param name="amount">The proposed new minimum proposal amount.</param>
    /// <param name="description">A description of the proposal, limited to 200 characters, preferably a link.</param>
    /// <returns>The Id of the generated proposal.</returns>
    ulong CreateProposalMinimumProposal(UInt256 amount, string description);

    /// <summary>
    /// Pledge for a proposal by temporarily locking CRS tokens to help meet the minimum pledge amount to move to an official vote.
    /// </summary>
    /// <param name="proposalId">The Id of the proposal to pledge to.</param>
    void Pledge(ulong proposalId);

    /// <summary>
    /// Votes for or against a proposal by temporarily holding CRS in contract as vote weight.
    /// </summary>
    /// <param name="proposalId">The Id number of the proposal being voted on.</param>
    /// <param name="inFavor">True or false indicating if the vote is in favor of the proposal or against it.</param>
    void Vote(ulong proposalId, bool inFavor);

    /// <summary>
    /// Withdraws held CRS from proposal vote, removing the vote if still in progress.
    /// </summary>
    /// <param name="proposalId">The Id number of the proposal voted on.</param>
    /// <param name="withdrawAmount">The amount of tokens to withdraw from the proposal vote.</param>
    void WithdrawVote(ulong proposalId, ulong withdrawAmount);

    /// <summary>
    /// Withdraw CRS tokens from a proposal pledge, removing the pledge if still in progress.
    /// </summary>
    /// <param name="proposalId">The Id of the proposal to remove a pledge amount from.</param>
    /// <param name="withdrawAmount">The amount to withdraw from a pledge.</param>
    void WithdrawPledge(ulong proposalId, ulong withdrawAmount);

    /// <summary>
    /// Completes a proposal and executes the resulting command within the Vault contract if approved.
    /// </summary>
    /// <param name="proposalId">The Id number of the proposal voted on.</param>
    void CompleteProposal(ulong proposalId);
}
