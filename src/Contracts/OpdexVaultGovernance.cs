using Stratis.SmartContracts;

/// Todo: Consider adding all of this to the existing Vault contract.
/// <summary>
/// A Governance contract that controls the Opdex Vault and its locked certificates.
/// </summary>
public class OpdexVaultGovernance : SmartContract, IOpdexVaultGovernance
{
    private const string RevokeProposalType = "Revoke";
    private const string CreateProposalType = "Create";
    private const ulong OneWeek = 60 * 60 * 24 * 7 / 16;

    /// <summary>
    /// Constructor to instantiate Opdex Vault Governance contract.
    /// </summary>
    /// <param name="contractState">Smart contract state.</param>
    /// <param name="vault">The Opdex Vault contract address this governance contract will claim ownership of.</param>
    public OpdexVaultGovernance(ISmartContractState contractState, Address vault) : base(contractState)
    {
        NextProposalId = 1;
        Vault = vault;
    }

    /// <inheritdoc />
    public Address Vault
    {
        get => State.GetAddress(nameof(Vault));
        private set => State.SetAddress(nameof(Vault), value);
    }

    /// <inheritdoc />
    public UInt256 NextProposalId
    {
        get => State.GetUInt256(nameof(NextProposalId));
        private set => State.SetUInt256(nameof(NextProposalId), value);
    }

    /// <inheritdoc />
    public VaultProposalDetails GetProposal(UInt256 id)
    {
        return State.GetStruct<VaultProposalDetails>($"Proposal:{id}");
    }

    private void SetProposal(VaultProposalDetails proposal)
    {
        State.SetStruct($"Proposal:{proposal.Id}", proposal);
    }

    /// <inheritdoc />
    public VaultProposalVote GetProposalVote(UInt256 proposalId, Address voter)
    {
        return State.GetStruct<VaultProposalVote>($"ProposalVoteWeight:{proposalId}:{voter}");
    }

    private void SetProposalVote(UInt256 proposalId, Address voter, VaultProposalVote vote)
    {
        State.SetStruct($"ProposalVoteWeight:{proposalId}:{voter}", vote);
    }

    // Todo: Consider limiting to 1 proposal at a time
    // -- Else multiple is fine, but users must spread their CRS to vote for all
    // -- To enable a re-usable wallet staking balance, would need to limit # of concurrent votes and track
    // -- -- This would be similar to a "staking balance" however, withdrawal would need to adjust all open proposals
    // -- -- Tracking all proposals and associated votes to a wallet's staking balance adds a lot of work / logic
    // Todo: Maybe no revocation with 1 year lockup.
    // Todo: Maybe make the user enter a ProposalId and validate one doesn't exist
    // -- The entered proposalId would be the # of a Github PR
    // -- Would create a repository dedicated to proposals so we can also see history of changes/agreements and comments.
    // -- Could then remove the proposal string and expect details at the github repo that the UI would pull
    /// <inheritdoc />
    public void Create(UInt256 amount, string proposal, Address holder, string type)
    {
        Assert(type == RevokeProposalType || type == CreateProposalType, "OPDEX: INVALID_PROPOSAL_TYPE");

        var certificatesResponse = Call(Vault, 0ul, nameof(IOpdexVault.GetCertificate), new object[] { holder });
        var certificate = (VaultCertificate)certificatesResponse.ReturnValue;

        if (type == RevokeProposalType)
        {
            Assert(!certificate.Revoked && certificate.Amount == amount, "OPDEX: INVALID_REVOKE_PROPOSAL");
        }
        else
        {
            Assert(certificate.Amount == UInt256.Zero && certificate.VestedBlock == 0ul, "OPDEX: INVALID_CREATE_PROPOSAL");

            var supplyResponse = Call(Vault, 0ul, nameof(IOpdexVault.TotalSupply));
            var vaultSupply = (UInt256)supplyResponse.ReturnValue;

            // Todo: Maybe also track ProposedAmount to Assert that (VaultTotalSupply - ProposedAmount > CreateAmount)
            // -- Would remove possible overflow of succeeding proposal(s) and amounts w/out sufficient vault supply

            Assert(supplyResponse.Success && vaultSupply > amount, "OPDEX: INSUFFICIENT_VAULT_SUPPLY");
        }

        var nextProposalId = NextProposalId;

        var details = new VaultProposalDetails
        {
            Amount = amount,
            Proposal = proposal,
            Holder = holder,
            CreatedBy = Message.Sender,
            Id = nextProposalId,
            Type = type,
            EndBlock = Block.Number + OneWeek
        };

        SetProposal(details);

        Log(details);

        NextProposalId = nextProposalId + 1;
    }

    // Todo: Consider allowing a vote switch
    // -- Current implementation requires stacking onto an existing vote, in favor or against
    // -- In order to switch the vote, users must withdraw from existing, then re-vote
    // -- Potentially enable a vote switch within the Vote call
    /// <inheritdoc />
    public void Vote(UInt256 proposalId, bool inFavor)
    {
        var proposal = GetProposal(proposalId);

        Assert(Block.Number <= proposal.EndBlock, "OPDEX: PROPOSAL_CLOSED");
        Assert(proposal.Id > UInt256.Zero, "OPDEX: INVALID_PROPOSAL");

        var proposalVote = GetProposalVote(proposalId, Message.Sender);
        var addedWeight = Message.Value;
        var previouslyVoted = proposalVote.Weight > UInt256.Zero;

        if (inFavor)
        {
            if (previouslyVoted) Assert(proposalVote.InFavor, "OPDEX: ALREADY_VOTED_NOT_IN_FAVOR");
            proposal.YesWeight += addedWeight;
        }
        else
        {
            if (previouslyVoted) Assert(!proposalVote.InFavor, "OPDEX: ALREADY_VOTED_IN_FAVOR");
            proposal.NoWeight += addedWeight;
        }

        proposalVote.Weight += addedWeight;
        proposalVote.InFavor = inFavor;

        SetProposalVote(proposalId, Message.Sender, proposalVote);
    }

    /// <inheritdoc />
    public void Withdraw(UInt256 proposalId, ulong amount)
    {
        var proposal = GetProposal(proposalId);
        var proposalVote = GetProposalVote(proposalId, Message.Sender);

        Assert(amount <= proposalVote.Weight, "OPDEX: INSUFFICIENT_FUNDS");

        // Proposal is in progress, deduct their withdrawn vote
        if (proposal.EndBlock <= Block.Number)
        {
            if (proposalVote.InFavor) proposal.YesWeight -= amount;
            else proposal.NoWeight -= amount;

            SetProposal(proposal);
        }

        proposalVote.Weight -= amount;
        SetProposalVote(proposalId, Message.Sender, proposalVote);
        Transfer(Message.Sender, amount);
    }

    /// <inheritdoc />
    public void Complete(UInt256 proposalId)
    {
        var proposal = GetProposal(proposalId);
        if (proposal.Id == UInt256.Zero) return;

        Assert(proposal.Completed == false, "OPDEX: PROPOSAL_ALREADY_COMPLETE");
        Assert(Block.Number > proposal.EndBlock, "OPDEX: PROPOSAL_IN_PROGRESS");
        Assert(proposal.YesWeight > proposal.NoWeight, "OPDEX: PROPOSAL_DENIED");

        var response = proposal.Type == RevokeProposalType
            ? Call(Vault, 0, nameof(IOpdexVault.RevokeCertificate), new object[] { proposal.Holder })
            : Call(Vault, 0, nameof(IOpdexVault.CreateCertificate), new object[] { proposal.Holder, proposal.Amount });

        Assert(response.Success, "OPDEX: INVALID_PROPOSAL_CERTIFICATION");

        proposal.Completed = true;
        SetProposal(proposal);
    }
}
