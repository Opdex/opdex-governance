// Todo: Create or revoke proposals need to enforce one proposal per holder at a time
// Resolves potential overlap with incomplete CompleteProposal calls due to concurrent votes for the same holder
// -- One certificate proposal per existing/potential certificate holder active at a time
// Todo: Move all proposals of any type to Complete after expiration
// -- Potentially execute this when appropriate during Withdraw Methods
// -- Allow the CompleteProposal method to work with expired vote AND pledge statuses
// -- This is primarily to finalize and clear any flags indicating a holder has an open proposal.

using Stratis.SmartContracts;

/// <summary>
/// A smart contract that locks tokens for a specified vesting period.
/// </summary>
public class OpdexVault : SmartContract, IOpdexVault
{
    private const ulong OneDay = 60 * 60 * 24 / 16;
    private const ulong ThreeDays = OneDay * 3;
    private const ulong OneWeek = OneDay * 7;
    // Todo: This is to protect from a proposal setting a number above Max CRS supply
    // -- But maybe that is ok if it passes, it passes. The vault would officially be
    // -- retired w/ remaining tokens burned due to unachievable conditions
    private const ulong OneHundredMillionSats = 10_000_000_000_000_000;

    /// <summary>
    /// Constructor initializing an empty vault for locking tokens to be vested.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="token">The locked SRC token.</param>
    /// <param name="vestingDuration">The length in blocks of the vesting period.</param>
    /// <param name="pledgeMinimum">The minimum total number of token sin satoshis for a proposal to move to a vote.</param>
    /// <param name="proposalMinimum">The minimum total number of tokens in satoshis for a proposal to be valid.</param>
    public OpdexVault(ISmartContractState state, Address token, ulong vestingDuration, ulong pledgeMinimum, ulong proposalMinimum) : base(state)
    {
        Token = token;
        VestingDuration = vestingDuration;
        PledgeMinimum = pledgeMinimum;
        ProposalMinimum = proposalMinimum;
        NextProposalId = 1;
    }

    /// <inheritdoc />
    public ulong VestingDuration
    {
        get => State.GetUInt64(VaultStateKeys.VestingDuration);
        private set => State.SetUInt64(VaultStateKeys.VestingDuration, value);
    }

    /// <inheritdoc />
    public Address Token
    {
        get => State.GetAddress(VaultStateKeys.Token);
        private set => State.SetAddress(VaultStateKeys.Token, value);
    }

    /// <inheritdoc />
    public UInt256 TotalSupply
    {
        get => State.GetUInt256(VaultStateKeys.TotalSupply);
        private set => State.SetUInt256(VaultStateKeys.TotalSupply, value);
    }

    /// <inheritdoc />
    public UInt256 NextProposalId
    {
        get => State.GetUInt256(VaultStateKeys.NextProposalId);
        private set => State.SetUInt256(VaultStateKeys.NextProposalId, value);
    }

    /// <inheritdoc />
    public UInt256 ProposedAmount
    {
        get => State.GetUInt256(VaultStateKeys.ProposedAmount);
        private set => State.SetUInt256(VaultStateKeys.ProposedAmount, value);
    }

    /// <inheritdoc />
    public ulong PledgeMinimum
    {
        get => State.GetUInt64(VaultStateKeys.PledgeMinimum);
        private set => State.SetUInt64(VaultStateKeys.PledgeMinimum, value);
    }

    /// <inheritdoc />
    public ulong ProposalMinimum
    {
        get => State.GetUInt64(VaultStateKeys.ProposalMinimum);
        private set => State.SetUInt64(VaultStateKeys.ProposalMinimum, value);
    }

    /// <inheritdoc />
    public VaultCertificate GetCertificate(Address wallet)
    {
        return State.GetStruct<VaultCertificate>($"{VaultStateKeys.Certificate}:{wallet}");
    }

    private void SetCertificate(Address wallet, VaultCertificate certificate)
    {
        State.SetStruct($"{VaultStateKeys.Certificate}:{wallet}", certificate);
    }

    /// <inheritdoc />
    public VaultProposalDetails GetProposal(UInt256 proposalId)
    {
        return State.GetStruct<VaultProposalDetails>($"{VaultStateKeys.Proposal}:{proposalId}");
    }

    private void SetProposal(UInt256 proposalId, VaultProposalDetails proposal)
    {
        State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);
    }

    /// <inheritdoc />
    public VaultProposalVote GetProposalVote(UInt256 proposalId, Address voter)
    {
        return State.GetStruct<VaultProposalVote>($"{VaultStateKeys.ProposalVote}:{proposalId}:{voter}");
    }

    private void SetProposalVote(UInt256 proposalId, Address voter, VaultProposalVote vote)
    {
        State.SetStruct($"{VaultStateKeys.ProposalVote}:{proposalId}:{voter}", vote);
    }

    /// <inheritdoc />
    public ulong GetProposalPledge(UInt256 proposalId, Address voter)
    {
        return State.GetUInt64($"{VaultStateKeys.ProposalPledge}:{proposalId}:{voter}");
    }

    private void SetProposalPledge(UInt256 proposalId, Address voter, ulong amount)
    {
        State.SetUInt64($"{VaultStateKeys.ProposalPledge}:{proposalId}:{voter}", amount);
    }

    /// <inheritdoc />
    public void NotifyDistribution(UInt256 amount)
    {
        Assert(Message.Sender == Token, "OPDEX: UNAUTHORIZED");

        TotalSupply += amount;
    }

    // Todo: Consider breaking this out - proposer is not necessary for minimum amount proposals
    // -- UInt256 CreateCertificateProposal(UInt256 amount, Address proposer, string description, byte type)
    // -- UInt256 CreateMinimumAmountProposal(UInt256 amount, string description, byte type)
    /// <inheritdoc />
    public UInt256 CreateProposal(UInt256 amount, Address recipient, string description, byte type)
    {
        Assert(!string.IsNullOrWhiteSpace(description) && description.Length <= 200, "OPDEX: INVALID_DESCRIPTION");
        Assert(amount > 0, "OPDEX: INVALID_AMOUNT");

        var proposalType = (VaultProposalType)type;

        if (proposalType == VaultProposalType.Create)
        {
            var certificate = GetCertificate(recipient);
            var vaultSupply = TotalSupply;
            var proposedAmount = ProposedAmount;

            Assert(certificate.VestedBlock == 0ul, "OPDEX: CERTIFICATE_EXISTS");
            Assert(vaultSupply - proposedAmount >= amount, "OPDEX: INSUFFICIENT_VAULT_SUPPLY");

            ProposedAmount = proposedAmount + amount;
        }
        else if (proposalType == VaultProposalType.Revoke)
        {
            var certificate = GetCertificate(recipient);
            var maxExpiration = Block.Number + ThreeDays + OneWeek;

            Assert(!certificate.Revoked && certificate.VestedBlock > maxExpiration && certificate.Amount == amount, "OPDEX: INVALID_CERTIFICATE");
        }
        else if (proposalType == VaultProposalType.PledgeMinimum || proposalType == VaultProposalType.ProposalMinimum)
        {
            Assert(amount <= OneHundredMillionSats, "OPDEX: EXCESSIVE_AMOUNT");
        }
        else
        {
            Assert(false, "OPDEX: INVALID_PROPOSAL_TYPE");
        }

        var proposalId = NextProposalId;
        NextProposalId += 1;

        // Todo: Consider breaking this out into separate key values to save on gas, we don't always need everything
        // -- Description specifically, but also others such as YesAmount, NoAmount, PledgeAmount - Maybe wait to test gas costs
        var details = new VaultProposalDetails
        {
            Recipient = recipient,
            Amount = amount,
            Description = description,
            Type = proposalType,
            Status = VaultProposalStatus.Pledge,
            Expiration = Block.Number + OneWeek
        };

        SetProposal(proposalId, details);

        Log(new CreateVaultProposalLog
        {
            Id = proposalId,
            Recipient = details.Recipient,
            Amount = details.Amount,
            Description = details.Description,
            Type = details.Type,
            Status = details.Status,
            Expiration = details.Expiration
        });

        return proposalId;
    }

    /// <inheritdoc />
    public void Pledge(UInt256 proposalId)
    {
        var proposal = GetProposal(proposalId);

        Assert(proposal.Status == VaultProposalStatus.Pledge, "OPDEX: INVALID_STATUS");
        Assert(proposal.Expiration >= Block.Number, "OPDEX: PROPOSAL_EXPIRED");

        proposal.PledgeAmount += Message.Value;

        if (proposal.PledgeAmount >= PledgeMinimum)
        {
            proposal.Status = VaultProposalStatus.Vote;
            proposal.Expiration = Block.Number + ThreeDays;
        }

        var totalWalletPledgedAmount = GetProposalPledge(proposalId, Message.Sender) + Message.Value;

        SetProposalPledge(proposalId, Message.Sender, totalWalletPledgedAmount);
        SetProposal(proposalId, proposal);

        // Todo: maybe also the status of the outcome since it could change Pledge to Vote
        Log(new VaultProposalPledgeLog
        {
            ProposalId = proposalId,
            Wallet = Message.Sender,
            PledgeAmount = Message.Value,
            PledgerAmount = totalWalletPledgedAmount,
            ProposalPledgeAmount = proposal.PledgeAmount
        });
    }

    /// <inheritdoc />
    public void ProposalVote(UInt256 proposalId, bool inFavor)
    {
        var proposal = GetProposal(proposalId);

        Assert(proposal.Status == VaultProposalStatus.Vote, "OPDEX: INVALID_STATUS");
        Assert(Block.Number <= proposal.Expiration, "OPDEX: PROPOSAL_EXPIRED");

        var proposalVote = GetProposalVote(proposalId, Message.Sender);
        var previouslyVoted = proposalVote.Amount > UInt256.Zero;

        if (inFavor)
        {
            if (previouslyVoted) Assert(proposalVote.InFavor, "OPDEX: ALREADY_VOTED_NOT_IN_FAVOR");
            proposal.YesAmount += Message.Value;
        }
        else
        {
            if (previouslyVoted) Assert(!proposalVote.InFavor, "OPDEX: ALREADY_VOTED_IN_FAVOR");
            proposal.NoAmount += Message.Value;
        }

        proposalVote.Amount += Message.Value;
        proposalVote.InFavor = inFavor;

        SetProposalVote(proposalId, Message.Sender, proposalVote);
        SetProposal(proposalId, proposal);

        Log(new VaultProposalVoteLog
        {
            ProposalId = proposalId,
            Voter = Message.Sender,
            InFavor = inFavor,
            VoteAmount = Message.Value,
            VoterAmount = proposalVote.Amount,
            ProposalYesAmount = proposal.YesAmount,
            ProposalNoAmount = proposal.NoAmount
        });
    }

    /// <inheritdoc />
    public void ProposalVoteWithdraw(UInt256 proposalId, ulong withdrawAmount)
    {
        var proposal = GetProposal(proposalId);
        var proposalVote = GetProposalVote(proposalId, Message.Sender);

        Assert(withdrawAmount <= proposalVote.Amount, "OPDEX: INSUFFICIENT_FUNDS");

        // Todo: The proposal is still active, we might want to log and indicate that a vote was withdrawn
        // -- Would allow clients to index logs and understand the difference between withdrawals
        if (proposal.Expiration >= Block.Number && proposal.Status == VaultProposalStatus.Vote)
        {
            if (proposalVote.InFavor) proposal.YesAmount -= withdrawAmount;
            else proposal.NoAmount -= withdrawAmount;

            SetProposal(proposalId, proposal);
        }

        var remainingVoteAmount = proposalVote.Amount - withdrawAmount;
        proposalVote.Amount = remainingVoteAmount;

        SetProposalVote(proposalId, Message.Sender, proposalVote);
        SafeTransfer(Message.Sender, withdrawAmount);

        Log(new VaultProposalVoteWithdrawLog
        {
            ProposalId = proposalId,
            Voter = Message.Sender,
            WithdrawAmount = withdrawAmount,
            VoterAmount = remainingVoteAmount,
            ProposalYesAmount = proposal.YesAmount,
            ProposalNoAmount = proposal.NoAmount
        });
    }

    /// <inheritdoc />
    public void ProposalPledgeWithdraw(UInt256 proposalId, ulong withdrawAmount)
    {
        var proposal = GetProposal(proposalId);
        var pledge = GetProposalPledge(proposalId, Message.Sender);

        Assert(withdrawAmount <= pledge, "OPDEX: INSUFFICIENT_FUNDS");

        // Todo: The pledge is still active, we might want to log and indicate that a vote was withdrawn
        // -- Would allow clients to index logs and understand the difference between withdrawals
        if (proposal.Expiration >= Block.Number && proposal.Status == VaultProposalStatus.Pledge)
        {
            proposal.PledgeAmount -= withdrawAmount;
            SetProposal(proposalId, proposal);
        }

        var remainingPledgeAmount = pledge - withdrawAmount;

        SetProposalPledge(proposalId, Message.Sender, remainingPledgeAmount);
        SafeTransfer(Message.Sender, withdrawAmount);

        Log(new VaultProposalPledgeWithdrawLog
        {
            ProposalId = proposalId,
            Voter = Message.Sender,
            WithdrawAmount = withdrawAmount,
            PledgerAmount = remainingPledgeAmount,
            ProposalPledgeAmount = proposal.PledgeAmount
        });
    }

    /// <inheritdoc />
    public void CompleteProposal(UInt256 proposalId)
    {
        var proposal = GetProposal(proposalId);

        Assert(proposal.Status == VaultProposalStatus.Vote, "OPDEX: INVALID_STATUS");
        Assert(Block.Number > proposal.Expiration, "OPDEX: PROPOSAL_IN_PROGRESS");

        var totalVoteAmount = proposal.YesAmount + proposal.NoAmount;
        var approved = proposal.YesAmount > proposal.NoAmount && totalVoteAmount >= ProposalMinimum;

        if (approved)
        {
            if (proposal.Type == VaultProposalType.Create) CreateCertificate(proposal.Recipient, proposal.Amount);
            else if (proposal.Type == VaultProposalType.Revoke) RevokeCertificate(proposal.Recipient);
            else if (proposal.Type == VaultProposalType.PledgeMinimum) PledgeMinimum = (ulong)proposal.Amount;
            else ProposalMinimum = (ulong)proposal.Amount;
        }

        proposal.Status = VaultProposalStatus.Complete;
        SetProposal(proposalId, proposal);

        if (proposal.Type == VaultProposalType.Create) ProposedAmount -= proposal.Amount;

        Log(new CompleteVaultProposalLog { ProposalId = proposalId, Approved = approved });
    }

    /// <inheritdoc />
    public void RedeemCertificate()
    {
        var certificate = GetCertificate(Message.Sender);

        Assert(certificate.VestedBlock > 0, "OPDEX: CERTIFICATE_NOT_FOUND");
        Assert(certificate.VestedBlock < Block.Number, "OPDEX: CERTIFICATE_VESTING");

        SetCertificate(Message.Sender, default(VaultCertificate));

        SafeTransferTo(Token, Message.Sender, certificate.Amount);

        Log(new RedeemVaultCertificateLog {Owner = Message.Sender, Amount = certificate.Amount, VestedBlock = certificate.VestedBlock});
    }

    private void CreateCertificate(Address to, UInt256 amount)
    {
        Assert(amount > 0 && amount <= TotalSupply, "OPDEX: INVALID_AMOUNT");

        var certificate = GetCertificate(to);

        Assert(certificate.Amount == UInt256.Zero, "OPDEX: CERTIFICATE_EXISTS");

        var vestedBlock = Block.Number + VestingDuration;

        certificate = new VaultCertificate { Amount = amount, VestedBlock = vestedBlock, Revoked = false };

        SetCertificate(to, certificate);

        TotalSupply -= amount;

        Log(new CreateVaultCertificateLog{ Owner = to, Amount = amount, VestedBlock = vestedBlock });
    }

    private void RevokeCertificate(Address wallet)
    {
        var certificate = GetCertificate(wallet);

        Assert(!certificate.Revoked, "OPDEX: CERTIFICATE_REVOKED");
        Assert(certificate.VestedBlock >= Block.Number, "OPDEX: CERTIFICATE_VESTED");

        var vestingDuration = VestingDuration;
        var vestingAmount = certificate.Amount;
        var vestingBlock = certificate.VestedBlock - vestingDuration;
        var vestedBlocks = Block.Number - vestingBlock;

        UInt256 percentageOffset = 100;

        var divisor = vestingDuration * percentageOffset / vestedBlocks;
        var newAmount = vestingAmount * percentageOffset / divisor;

        certificate.Amount = newAmount;
        certificate.Revoked = true;

        SetCertificate(wallet, certificate);

        TotalSupply += (vestingAmount - newAmount);

        Log(new RevokeVaultCertificateLog {Owner = wallet, OldAmount = vestingAmount, NewAmount = newAmount, VestedBlock = certificate.VestedBlock});
    }

    private void SafeTransferTo(Address token, Address to, UInt256 amount)
    {
        if (amount == 0) return;

        var result = Call(token, 0, nameof(IOpdexMinedToken.TransferTo), new object[] {to, amount});

        Assert(result.Success && (bool)result.ReturnValue, "OPDEX: INVALID_TRANSFER_TO");
    }

    private void SafeTransfer(Address to, ulong amount)
    {
        if (amount == 0) return;

        Assert(Transfer(to, amount).Success, "OPDEX: INVALID_TRANSFER");
    }
}
