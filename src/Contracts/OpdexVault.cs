using Stratis.SmartContracts;

/// <summary>
/// A smart contract that locks tokens only released through successful proposals and after a vesting period.
/// </summary>
public class OpdexVault : SmartContract, IOpdexVault
{
    private const ulong OneDay = 60 * 60 * 24 / 16;
    private const ulong VoteDuration = OneDay * 3;
    private const ulong PledgeDuration = OneDay * 7;

    /// <summary>
    /// Constructor initializing an empty vault smart contract.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="token">The locked SRC token.</param>
    /// <param name="vestingDuration">The length in blocks of the vesting period.</param>
    /// <param name="pledgeMinimum">The minimum total number of tokens in satoshis for a proposal to move to a vote.</param>
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
    public UInt256 TotalProposedAmount
    {
        get => State.GetUInt256(VaultStateKeys.TotalProposedAmount);
        private set => State.SetUInt256(VaultStateKeys.TotalProposedAmount, value);
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
    public Certificate GetCertificate(Address wallet)
    {
        return State.GetStruct<Certificate>($"{VaultStateKeys.Certificate}:{wallet}");
    }

    private void SetCertificate(Address wallet, Certificate certificate)
    {
        State.SetStruct($"{VaultStateKeys.Certificate}:{wallet}", certificate);
    }

    /// <inheritdoc />
    public ProposalDetails GetProposal(UInt256 proposalId)
    {
        return State.GetStruct<ProposalDetails>($"{VaultStateKeys.Proposal}:{proposalId}");
    }

    private void SetProposal(UInt256 proposalId, ProposalDetails proposal)
    {
        State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);
    }

    /// <inheritdoc />
    public ProposalVote GetProposalVote(UInt256 proposalId, Address voter)
    {
        return State.GetStruct<ProposalVote>($"{VaultStateKeys.ProposalVote}:{proposalId}:{voter}");
    }

    private void SetProposalVote(UInt256 proposalId, Address voter, ProposalVote vote)
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
    public UInt256 GetCertificateProposalIdByRecipient(Address recipient)
    {
        return State.GetUInt256($"{VaultStateKeys.ProposalIdByRecipient}:{recipient}");
    }

    private void SetCertificateProposalIdByRecipient(Address recipient, UInt256 proposalId)
    {
        State.SetUInt256($"{VaultStateKeys.ProposalIdByRecipient}:{recipient}", proposalId);
    }

    /// <inheritdoc />
    public void NotifyDistribution(UInt256 amount)
    {
        Assert(Message.Sender == Token, "OPDEX: UNAUTHORIZED");

        TotalSupply += amount;
    }

    /// <inheritdoc />
    public UInt256 CreateNewCertificateProposal(UInt256 amount, Address recipient, string description)
    {
        ValidateNewProposalBaseDetails(amount, description);

        var certificate = GetCertificate(recipient);

        Assert(certificate.VestedBlock == 0ul, "OPDEX: CERTIFICATE_EXISTS");

        var proposedAmount = TotalProposedAmount;

        Assert(TotalSupply - proposedAmount >= amount, "OPDEX: INSUFFICIENT_VAULT_SUPPLY");

        TotalProposedAmount = proposedAmount + amount;

        return CreateCertificateTypeProposalExecute(recipient, amount, description, ProposalType.Create);
    }

    /// <inheritdoc />
    public UInt256 CreateRevokeCertificateProposal(UInt256 amount, Address recipient, string description)
    {
        ValidateNewProposalBaseDetails(amount, description);

        var certificate = GetCertificate(recipient);
        var maxExpiration = Block.Number + VoteDuration + PledgeDuration;

        Assert(!certificate.Revoked && certificate.VestedBlock > maxExpiration && certificate.Amount == amount, "OPDEX: INVALID_CERTIFICATE");

        return CreateCertificateTypeProposalExecute(recipient, amount, description, ProposalType.Revoke);
    }

    /// <inheritdoc />
    public UInt256 CreatePledgeMinimumProposal(UInt256 amount, string description)
    {
        ValidateNewProposalBaseDetails(amount, description);

        return CreateMinimumAmountChangeProposalExecute(amount, description, ProposalType.PledgeMinimum);
    }

    /// <inheritdoc />
    public UInt256 CreateProposalMinimumProposal(UInt256 amount, string description)
    {
        ValidateNewProposalBaseDetails(amount, description);

        return CreateMinimumAmountChangeProposalExecute(amount, description, ProposalType.ProposalMinimum);
    }

    /// <inheritdoc />
    public void Pledge(UInt256 proposalId)
    {
        var proposal = GetProposal(proposalId);

        Assert(proposal.Status == ProposalStatus.Pledge, "OPDEX: INVALID_STATUS");
        Assert(proposal.Expiration >= Block.Number, "OPDEX: PROPOSAL_EXPIRED");

        proposal.PledgeAmount += Message.Value;

        var pledgeMinimumMet = proposal.PledgeAmount >= PledgeMinimum;
        if (proposal.PledgeAmount >= PledgeMinimum)
        {
            proposal.Status = ProposalStatus.Vote;
            proposal.Expiration = Block.Number + VoteDuration;
        }

        var totalWalletPledgedAmount = GetProposalPledge(proposalId, Message.Sender) + Message.Value;

        SetProposalPledge(proposalId, Message.Sender, totalWalletPledgedAmount);
        SetProposal(proposalId, proposal);

        Log(new VaultProposalPledgeLog
        {
            ProposalId = proposalId,
            Wallet = Message.Sender,
            PledgeAmount = Message.Value,
            PledgerAmount = totalWalletPledgedAmount,
            ProposalPledgeAmount = proposal.PledgeAmount,
            PledgeMinimumMet = pledgeMinimumMet
        });
    }

    /// <inheritdoc />
    public void Vote(UInt256 proposalId, bool inFavor)
    {
        var proposal = GetProposal(proposalId);

        Assert(proposal.Status == ProposalStatus.Vote, "OPDEX: INVALID_STATUS");
        Assert(proposal.Expiration >= Block.Number, "OPDEX: PROPOSAL_EXPIRED");

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
    public void VoteWithdraw(UInt256 proposalId, ulong withdrawAmount)
    {
        var proposal = GetProposal(proposalId);
        var proposalVote = GetProposalVote(proposalId, Message.Sender);

        Assert(withdrawAmount <= proposalVote.Amount, "OPDEX: INSUFFICIENT_FUNDS");

        var proposalIsActive = proposal.Expiration >= Block.Number;
        var voteWithdrawn = proposalIsActive && proposal.Status == ProposalStatus.Vote;

        if (voteWithdrawn)
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
            ProposalNoAmount = proposal.NoAmount,
            VoteWithdrawn = voteWithdrawn
        });

        if (!proposalIsActive && proposal.Status != ProposalStatus.Complete) CompleteProposalExecute(proposalId, proposal);
    }

    /// <inheritdoc />
    public void PledgeWithdraw(UInt256 proposalId, ulong withdrawAmount)
    {
        var proposal = GetProposal(proposalId);
        var pledge = GetProposalPledge(proposalId, Message.Sender);

        Assert(withdrawAmount <= pledge, "OPDEX: INSUFFICIENT_FUNDS");

        var proposalIsActive = proposal.Expiration >= Block.Number;
        var voteWithdrawn = proposalIsActive && proposal.Status == ProposalStatus.Pledge;

        if (voteWithdrawn)
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
            ProposalPledgeAmount = proposal.PledgeAmount,
            VoteWithdrawn = voteWithdrawn
        });

        if (!proposalIsActive && proposal.Status != ProposalStatus.Complete) CompleteProposalExecute(proposalId, proposal);
    }

    /// <inheritdoc />
    public void CompleteProposal(UInt256 proposalId)
    {
        var proposal = GetProposal(proposalId);

        Assert(proposal.Expiration > 0, "OPDEX: INVALID_PROPOSAL");
        Assert(proposal.Status == ProposalStatus.Vote || proposal.Status == ProposalStatus.Pledge, "OPDEX: INVALID_STATUS");
        Assert(Block.Number > proposal.Expiration, "OPDEX: PROPOSAL_IN_PROGRESS");

        CompleteProposalExecute(proposalId, proposal);
    }

    /// <inheritdoc />
    public void RedeemCertificate()
    {
        var certificate = GetCertificate(Message.Sender);

        Assert(certificate.VestedBlock > 0, "OPDEX: CERTIFICATE_NOT_FOUND");
        Assert(certificate.VestedBlock < Block.Number, "OPDEX: CERTIFICATE_VESTING");

        SetCertificate(Message.Sender, default(Certificate));

        SafeTransferTo(Token, Message.Sender, certificate.Amount);

        Log(new RedeemVaultCertificateLog {Owner = Message.Sender, Amount = certificate.Amount, VestedBlock = certificate.VestedBlock});
    }

    private void CreateCertificate(Address to, UInt256 amount)
    {
        var vestedBlock = Block.Number + VestingDuration;
        var certificate = new Certificate { Amount = amount, VestedBlock = vestedBlock, Revoked = false };

        SetCertificate(to, certificate);

        TotalSupply -= amount;

        Log(new CreateVaultCertificateLog{ Owner = to, Amount = amount, VestedBlock = vestedBlock });
    }

    private void RevokeCertificate(Address wallet)
    {
        var certificate = GetCertificate(wallet);

        // Manual triggers cause the possibility of revocation attempts on vested certificates
        if (certificate.VestedBlock < Block.Number) return;

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

    private UInt256 CreateCertificateTypeProposalExecute(Address recipient, UInt256 amount, string description, ProposalType type)
    {
        var existingProposalId = GetCertificateProposalIdByRecipient(recipient);

        Assert(existingProposalId == UInt256.Zero, "OPDEX: RECIPIENT_PROPOSAL_IN_PROGRESS");

        var proposalId = CreatProposalExecute(recipient, amount, description, type);

        SetCertificateProposalIdByRecipient(recipient, proposalId);

        return proposalId;
    }

    private UInt256 CreateMinimumAmountChangeProposalExecute(UInt256 amount, string description, ProposalType type)
    {
        ValidateProposalMinimumChangeAmount(amount);

        return CreatProposalExecute(Message.Sender, amount, description, type);
    }

    private UInt256 CreatProposalExecute(Address recipient, UInt256 amount, string description, ProposalType type)
    {
        const ProposalStatus status = ProposalStatus.Pledge;
        var proposalId = NextProposalId;
        var expiration = Block.Number + PledgeDuration;

        NextProposalId += 1;

        SetProposal(proposalId, new ProposalDetails
        {
            Wallet = recipient,
            Amount = amount,
            Type = type,
            Status = status,
            Expiration = expiration
        });

        Log(new CreateVaultProposalLog
        {
            Id = proposalId,
            Wallet = recipient,
            Amount = amount,
            Description = description,
            Type = type,
            Status = status,
            Expiration = expiration
        });

        return proposalId;
    }

    private void CompleteProposalExecute(UInt256 proposalId, ProposalDetails proposal)
    {
        var totalVoteAmount = proposal.YesAmount + proposal.NoAmount;
        var approved = proposal.Status == ProposalStatus.Vote && proposal.YesAmount > proposal.NoAmount && totalVoteAmount >= ProposalMinimum;

        if (approved)
        {
            if (proposal.Type == ProposalType.Create) CreateCertificate(proposal.Wallet, proposal.Amount);
            else if (proposal.Type == ProposalType.Revoke) RevokeCertificate(proposal.Wallet);
            else if (proposal.Type == ProposalType.PledgeMinimum) PledgeMinimum = (ulong)proposal.Amount;
            else ProposalMinimum = (ulong)proposal.Amount;
        }

        proposal.Status = ProposalStatus.Complete;
        SetProposal(proposalId, proposal);

        if (proposal.Type == ProposalType.Create || proposal.Type == ProposalType.Revoke)
        {
            SetCertificateProposalIdByRecipient(proposal.Wallet, UInt256.Zero);

            if (proposal.Type == ProposalType.Create) TotalProposedAmount -= proposal.Amount;
        }

        Log(new CompleteVaultProposalLog { ProposalId = proposalId, Approved = approved });
    }

    private void ValidateNewProposalBaseDetails(UInt256 amount, string description)
    {
        Assert(!string.IsNullOrWhiteSpace(description) && description.Length <= 200, "OPDEX: INVALID_DESCRIPTION");
        Assert(amount > 0, "OPDEX: INVALID_AMOUNT");
    }

    private void ValidateProposalMinimumChangeAmount(UInt256 amount)
    {
        Assert(amount <= ulong.MaxValue, "OPDEX: EXCESSIVE_AMOUNT");
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
