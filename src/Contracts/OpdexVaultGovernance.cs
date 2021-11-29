// Todo: Consider adding all of this to the existing Vault contract.
// Todo: Consider limiting number of active proposals to not spread votes thin, maybe 2-4?
// -- Consider also a first step, min # of CRS pledged to start the real vote. -- Maybe 5k CRS
// -- Does introduce problematic scenarios with CRS price fluctation.
// -- Could just Assert in Vote() that the minimum pledge is met
// -- -- Add a Pledge() method, accept CRS until the final pledge exceeds the minimum threshold
// -- -- -- When threshold is met, flag proposal, set end block, start the real vote
// -- -- -- Once the real vote is started, start at 0, pledges will need to withdraw then vote again
// Todo: Consider a minimum yes vote weight for a proposal to pass
// -- Maybe 10k - 25k CRS -- introduces complexities with CRS price fluctuation

using Stratis.SmartContracts;

/// <summary>
/// A Governance contract that controls the Opdex Vault and its locked certificates.
/// </summary>
public class OpdexVaultGovernance : SmartContract, IOpdexVaultGovernance
{
    private const ulong ThreeDayProposal = 60 * 60 * 24 * 3 / 16;

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
        get => State.GetAddress(VaultGovernanceStateKeys.Vault);
        private set => State.SetAddress(VaultGovernanceStateKeys.Vault, value);
    }

    /// <inheritdoc />
    public UInt256 NextProposalId
    {
        get => State.GetUInt256(VaultGovernanceStateKeys.NextProposalId);
        private set => State.SetUInt256(VaultGovernanceStateKeys.NextProposalId, value);
    }

    /// <inheritdoc />
    public UInt256 ProposedAmount
    {
        get => State.GetUInt256(VaultGovernanceStateKeys.ProposedAmount);
        private set => State.SetUInt256(VaultGovernanceStateKeys.ProposedAmount, value);
    }

    /// <inheritdoc />
    public VaultProposalDetails GetProposal(UInt256 id)
    {
        return State.GetStruct<VaultProposalDetails>($"{VaultGovernanceStateKeys.Proposal}:{id}");
    }

    private void SetProposal(UInt256 proposalId, VaultProposalDetails proposal)
    {
        State.SetStruct($"{VaultGovernanceStateKeys.Proposal}:{proposalId}", proposal);
    }

    /// <inheritdoc />
    public VaultProposalVote GetProposalVote(UInt256 proposalId, Address voter)
    {
        return State.GetStruct<VaultProposalVote>($"{VaultGovernanceStateKeys.ProposalVote}:{proposalId}:{voter}");
    }

    private void SetProposalVote(UInt256 proposalId, Address voter, VaultProposalVote vote)
    {
        State.SetStruct($"{VaultGovernanceStateKeys.ProposalVote}:{proposalId}:{voter}", vote);
    }

    /// <inheritdoc />
    public void Create(UInt256 amount, Address holder, string description, byte type)
    {
        var proposalType = (VaultProposalType)type;
        var isCreate = proposalType == VaultProposalType.Create;
        var isRevoke = proposalType == VaultProposalType.Revoke;

        Assert(isCreate || isRevoke, "OPDEX: INVALID_PROPOSAL_TYPE");
        Assert(description.Length <= 200, "OPDEX: EXCESSIVE_DESCRIPTION");

        var certificatesResponse = Call(Vault, 0ul, nameof(IOpdexVault.GetCertificate), new object[] { holder });
        var certificate = (VaultCertificate)certificatesResponse.ReturnValue;

        if (isRevoke)
        {
            Assert(!certificate.Revoked &&
                   certificate.VestedBlock > Block.Number + ThreeDayProposal &&
                   certificate.Amount == amount, "OPDEX: INVALID_REVOKE_PROPOSAL");
        }
        else
        {
            Assert(certificate.Amount == UInt256.Zero && certificate.VestedBlock == 0ul, "OPDEX: INVALID_CREATE_PROPOSAL");

            var supplyResponse = Call(Vault, 0ul, nameof(IOpdexVault.TotalSupply));
            var vaultSupply = (UInt256)supplyResponse.ReturnValue;
            var proposedAmount = ProposedAmount;

            Assert(supplyResponse.Success &&
                   vaultSupply > amount &&
                   vaultSupply - proposedAmount > amount, "OPDEX: INSUFFICIENT_VAULT_SUPPLY");

            ProposedAmount = proposedAmount + amount;
        }

        var proposalId = NextProposalId;
        NextProposalId += 1;

        var details = new VaultProposalDetails
        {
            Holder = holder,
            Amount = amount,
            Description = description, // Todo: Maybe don't persist this to save on gas
            Type = proposalType,
            EndBlock = Block.Number + ThreeDayProposal
        };

        SetProposal(proposalId, details);

        Log(details);
    }

    /// <inheritdoc />
    public void Vote(UInt256 proposalId, bool inFavor)
    {
        var proposal = GetProposal(proposalId);

        Assert(proposal.Amount > UInt256.Zero, "OPDEX: INVALID_PROPOSAL");
        Assert(Block.Number <= proposal.EndBlock, "OPDEX: PROPOSAL_CLOSED");

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

        Log(new VaultProposalVoteLog
        {
            ProposalId = proposalId,
            Voter = Message.Sender,
            InFavor = inFavor,
            Weight = addedWeight,
            TotalVoterWeight = proposalVote.Weight,
            TotalProposalYesWeight = proposal.YesWeight,
            TotalProposalNoWeight = proposal.NoWeight
        });
    }

    /// <inheritdoc />
    public void Withdraw(UInt256 proposalId, ulong amount)
    {
        var proposal = GetProposal(proposalId);
        var proposalVote = GetProposalVote(proposalId, Message.Sender);

        Assert(amount <= proposalVote.Weight, "OPDEX: INSUFFICIENT_FUNDS");

        // Todo: The proposal is still active, we might want to log and indicate that a vote was withdrawn
        // -- Would allow clients to index logs and understand the difference between withdrawals
        if (proposal.EndBlock <= Block.Number)
        {
            if (proposalVote.InFavor) proposal.YesWeight -= amount;
            else proposal.NoWeight -= amount;

            SetProposal(proposalId, proposal);
        }

        var remainingVoteWeight = proposalVote.Weight - amount;
        proposalVote.Weight = remainingVoteWeight;

        SetProposalVote(proposalId, Message.Sender, proposalVote);
        Transfer(Message.Sender, amount);

        Log(new VaultProposalVoteWithdrawalLog
        {
            ProposalId = proposalId,
            Voter = Message.Sender,
            Amount = amount,
            RemainingAmount = remainingVoteWeight,
            TotalProposalYesWeight = proposal.YesWeight,
            TotalProposalNoWeight = proposal.NoWeight
        });
    }

    /// <inheritdoc />
    public void Complete(UInt256 proposalId)
    {
        var proposal = GetProposal(proposalId);

        Assert(proposal.Amount > UInt256.Zero, "OPDEX: INVALID_PROPOSAL");
        Assert(proposal.Completed == false, "OPDEX: PROPOSAL_ALREADY_COMPLETE");
        Assert(Block.Number > proposal.EndBlock, "OPDEX: PROPOSAL_IN_PROGRESS");

        var approved = proposal.YesWeight > proposal.NoWeight;

        proposal.Completed = true;
        SetProposal(proposalId, proposal);

        var isRevocation = proposal.Type == VaultProposalType.Revoke;
        if (!isRevocation) ProposedAmount -= proposal.Amount;

        if (approved)
        {
            var response = isRevocation
                ? Call(Vault, 0, nameof(IOpdexVault.RevokeCertificate), new object[] { proposal.Holder })
                : Call(Vault, 0, nameof(IOpdexVault.CreateCertificate), new object[] { proposal.Holder, proposal.Amount });

            Assert(response.Success, "OPDEX: INVALID_PROPOSAL_CERTIFICATION");
        }

        Log(new VaultProposalCompleteLog { ProposalId = proposalId, Approved = approved });
    }
}
