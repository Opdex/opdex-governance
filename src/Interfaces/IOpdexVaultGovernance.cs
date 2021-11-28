using Stratis.SmartContracts;

public interface IOpdexVaultGovernance
{
    /// <summary>
    /// The address of the vault smart contract that this governance will own.
    /// </summary>
    Address Vault { get; }

    /// <summary>
    /// The Id of the next proposal to be created.
    /// </summary>
    UInt256 NextProposalId { get; }

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
    /// Creates a new proposal for a vault certificate to either be created or revoked.
    /// </summary>
    /// <param name="amount">The amount of tokens for a creation proposal.</param>
    /// <param name="proposal">A short description that includes a Github link to the full proposal.</param>
    /// <param name="holder">The receiving address of the proposed vault certificate.</param>
    /// <param name="type">The type of proposal to create, "Revoke" or "Create".</param>
    void Create(UInt256 amount, string proposal, Address holder, string type);

    /// <summary>
    /// Votes for or against a proposal by temporarily holding CRS in contract as vote weight.
    /// </summary>
    /// <param name="proposalId">The Id number of the proposal being voted on.</param>
    /// <param name="inFavor">True or false indicating if the vote is in favor of the proposal or against it.</param>
    void Vote(UInt256 proposalId, bool inFavor);


    /// <summary>
    /// Withdraws held CRS from proposal vote, removing the vote if still in progress.
    /// </summary>
    /// <param name="proposalId">The Id number of the proposal voted on.</param>
    /// <param name="amount">The amount of tokens to remove from the proposal vote.</param>
    void Withdraw(UInt256 proposalId, ulong amount);


    /// <summary>
    /// Completes a proposal and executes the resulting command within the Vault contract if passed, creating or revoking a vault certificate.
    /// </summary>
    /// <param name="proposalId">The Id number of the proposal voted on.</param>
    void Complete(UInt256 proposalId);
}
