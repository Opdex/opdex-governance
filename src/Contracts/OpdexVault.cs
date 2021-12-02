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
    /// <param name="totalPledgeMinimum">The minimum total number of tokens pledged to a proposal to move to a vote.</param>
    /// <param name="totalVoteMinimum">The minimum total number of tokens voted on a proposal to have a chance to be approved.</param>
    public OpdexVault(ISmartContractState state, Address token, ulong vestingDuration, ulong totalPledgeMinimum, ulong totalVoteMinimum) : base(state)
    {
        Token = token;
        VestingDuration = vestingDuration;
        TotalPledgeMinimum = totalPledgeMinimum;
        TotalVoteMinimum = totalVoteMinimum;
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
    public ulong NextProposalId
    {
        get => State.GetUInt64(VaultStateKeys.NextProposalId);
        private set => State.SetUInt64(VaultStateKeys.NextProposalId, value);
    }

    /// <inheritdoc />
    public UInt256 TotalProposedAmount
    {
        get => State.GetUInt256(VaultStateKeys.TotalProposedAmount);
        private set => State.SetUInt256(VaultStateKeys.TotalProposedAmount, value);
    }

    /// <inheritdoc />
    public ulong TotalPledgeMinimum
    {
        get => State.GetUInt64(VaultStateKeys.TotalPledgeMinimum);
        private set => State.SetUInt64(VaultStateKeys.TotalPledgeMinimum, value);
    }

    /// <inheritdoc />
    public ulong TotalVoteMinimum
    {
        get => State.GetUInt64(VaultStateKeys.TotalVoteMinimum);
        private set => State.SetUInt64(VaultStateKeys.TotalVoteMinimum, value);
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
    public ProposalDetails GetProposal(ulong proposalId)
    {
        return State.GetStruct<ProposalDetails>($"{VaultStateKeys.Proposal}:{proposalId}");
    }

    private void SetProposal(ulong proposalId, ProposalDetails proposal)
    {
        State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);
    }

    /// <inheritdoc />
    public ProposalVote GetProposalVote(ulong proposalId, Address voter)
    {
        return State.GetStruct<ProposalVote>($"{VaultStateKeys.ProposalVote}:{proposalId}:{voter}");
    }

    private void SetProposalVote(ulong proposalId, Address voter, ProposalVote vote)
    {
        State.SetStruct($"{VaultStateKeys.ProposalVote}:{proposalId}:{voter}", vote);
    }

    /// <inheritdoc />
    public ulong GetProposalPledge(ulong proposalId, Address pledger)
    {
        return State.GetUInt64($"{VaultStateKeys.ProposalPledge}:{proposalId}:{pledger}");
    }

    private void SetProposalPledge(ulong proposalId, Address pledger, ulong amount)
    {
        State.SetUInt64($"{VaultStateKeys.ProposalPledge}:{proposalId}:{pledger}", amount);
    }

    /// <inheritdoc />
    public ulong GetCertificateProposalIdByRecipient(Address recipient)
    {
        return State.GetUInt64($"{VaultStateKeys.ProposalIdByRecipient}:{recipient}");
    }

    private void SetCertificateProposalIdByRecipient(Address recipient, ulong proposalId)
    {
        State.SetUInt64($"{VaultStateKeys.ProposalIdByRecipient}:{recipient}", proposalId);
    }

    /// <inheritdoc />
    public void NotifyDistribution(UInt256 amount)
    {
        Assert(Message.Sender == Token, "OPDEX: UNAUTHORIZED");

        TotalSupply += amount;
    }

    /// <inheritdoc />
    public ulong CreateNewCertificateProposal(UInt256 amount, Address recipient, string description)
    {
        ValidateProposal(amount, description);

        var certificate = GetCertificate(recipient);

        Assert(certificate.VestedBlock == 0ul, "OPDEX: CERTIFICATE_EXISTS");

        var totalProposedAmount = TotalProposedAmount;

        Assert(TotalSupply - totalProposedAmount >= amount, "OPDEX: INSUFFICIENT_VAULT_SUPPLY");

        TotalProposedAmount = totalProposedAmount + amount;

        return CreateCertificateTypeProposalExecute(recipient, amount, description, (byte)ProposalType.Create);
    }

    /// <inheritdoc />
    public ulong CreateRevokeCertificateProposal(Address recipient, string description)
    {
        var certificate = GetCertificate(recipient);
        var maxExpiration = Block.Number + VoteDuration + PledgeDuration;

        Assert(!certificate.Revoked && certificate.VestedBlock > maxExpiration, "OPDEX: INVALID_CERTIFICATE");
        ValidateProposal(certificate.Amount, description);
        return CreateCertificateTypeProposalExecute(recipient, certificate.Amount, description, (byte)ProposalType.Revoke);
    }

    /// <inheritdoc />
    public ulong CreateTotalPledgeMinimumProposal(UInt256 amount, string description)
    {
        ValidateProposal(amount, description);
        return CreateMinimumAmountChangeProposalExecute(amount, description, (byte)ProposalType.TotalPledgeMinimum);
    }

    /// <inheritdoc />
    public ulong CreateTotalVoteMinimumProposal(UInt256 amount, string description)
    {
        ValidateProposal(amount, description);
        return CreateMinimumAmountChangeProposalExecute(amount, description, (byte)ProposalType.TotalVoteMinimum);
    }

    /// <inheritdoc />
    public void Pledge(ulong proposalId)
    {
        Assert(Message.Value > 0, "OPDEX: INSUFFICIENT_PLEDGE_AMOUNT");

        var proposal = GetProposal(proposalId);
        var proposalStatus = (ProposalStatus)proposal.Status;

        Assert(proposalStatus == ProposalStatus.Pledge, "OPDEX: INVALID_STATUS");
        Assert(proposal.Expiration >= Block.Number, "OPDEX: PROPOSAL_EXPIRED");

        proposal.PledgeAmount += Message.Value;

        var pledgeMinimumMet = proposal.PledgeAmount >= TotalPledgeMinimum;
        if (pledgeMinimumMet)
        {
            proposal.Status = (byte)ProposalStatus.Vote;
            proposal.Expiration = Block.Number + VoteDuration;
        }

        var totalWalletPledgedAmount = GetProposalPledge(proposalId, Message.Sender) + Message.Value;

        SetProposalPledge(proposalId, Message.Sender, totalWalletPledgedAmount);
        SetProposal(proposalId, proposal);

        Log(new VaultProposalPledgeLog
        {
            ProposalId = proposalId,
            Pledger = Message.Sender,
            PledgeAmount = Message.Value,
            PledgerAmount = totalWalletPledgedAmount,
            ProposalPledgeAmount = proposal.PledgeAmount,
            TotalPledgeMinimumMet = pledgeMinimumMet
        });
    }

    /// <inheritdoc />
    public void Vote(ulong proposalId, bool inFavor)
    {
        Assert(Message.Value > 0, "OPDEX: INSUFFICIENT_VOTE_AMOUNT");

        var proposal = GetProposal(proposalId);

        Assert((ProposalStatus)proposal.Status == ProposalStatus.Vote, "OPDEX: INVALID_STATUS");
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
    public void WithdrawVote(ulong proposalId, ulong withdrawAmount)
    {
        var proposalVote = GetProposalVote(proposalId, Message.Sender);
        var proposal = GetProposal(proposalId);
        var proposalStatus = (ProposalStatus)proposal.Status;
        var proposalIsActive = proposal.Expiration >= Block.Number;
        var voteWithdrawn = proposalIsActive && proposalStatus == ProposalStatus.Vote;
        var remainingVoteAmount = proposalVote.Amount - withdrawAmount;

        EnsureWithdrawalValidity(withdrawAmount, proposalVote.Amount);
        proposalVote.Amount = remainingVoteAmount;
        SetProposalVote(proposalId, Message.Sender, proposalVote);
        SafeTransfer(Message.Sender, withdrawAmount);

        if (voteWithdrawn)
        {
            if (proposalVote.InFavor) proposal.YesAmount -= withdrawAmount;
            else proposal.NoAmount -= withdrawAmount;

            SetProposal(proposalId, proposal);
        }

        Log(new VaultProposalWithdrawVoteLog
        {
            ProposalId = proposalId,
            Voter = Message.Sender,
            WithdrawAmount = withdrawAmount,
            VoterAmount = remainingVoteAmount,
            ProposalYesAmount = proposal.YesAmount,
            ProposalNoAmount = proposal.NoAmount,
            VoteWithdrawn = voteWithdrawn
        });

        if (!proposalIsActive && proposalStatus != ProposalStatus.Complete) CompleteProposalExecute(proposalId, proposal);
    }

    /// <inheritdoc />
    public void WithdrawPledge(ulong proposalId, ulong withdrawAmount)
    {
        var pledge = GetProposalPledge(proposalId, Message.Sender);
        var proposal = GetProposal(proposalId);
        var proposalIsActive = proposal.Expiration >= Block.Number;
        var voteWithdrawn = proposalIsActive && (ProposalStatus)proposal.Status == ProposalStatus.Pledge;
        var remainingPledgeAmount = pledge - withdrawAmount;

        EnsureWithdrawalValidity(withdrawAmount, pledge);
        SetProposalPledge(proposalId, Message.Sender, remainingPledgeAmount);
        SafeTransfer(Message.Sender, withdrawAmount);

        if (voteWithdrawn)
        {
            proposal.PledgeAmount -= withdrawAmount;
            SetProposal(proposalId, proposal);
        }

        Log(new VaultProposalWithdrawPledgeLog
        {
            ProposalId = proposalId,
            Pledger = Message.Sender,
            WithdrawAmount = withdrawAmount,
            PledgerAmount = remainingPledgeAmount,
            ProposalPledgeAmount = proposal.PledgeAmount,
            PledgeWithdrawn = voteWithdrawn
        });

        if (!proposalIsActive && (ProposalStatus)proposal.Status != ProposalStatus.Complete) CompleteProposalExecute(proposalId, proposal);
    }

    /// <inheritdoc />
    public void CompleteProposal(ulong proposalId)
    {
        EnsureNotPayable();
        var proposal = GetProposal(proposalId);
        var proposalStatus = (ProposalStatus)proposal.Status;

        Assert(proposal.Expiration > 0, "OPDEX: INVALID_PROPOSAL");
        Assert(proposalStatus == ProposalStatus.Vote || proposalStatus == ProposalStatus.Pledge, "OPDEX: ALREADY_COMPLETE");
        Assert(Block.Number > proposal.Expiration, "OPDEX: PROPOSAL_IN_PROGRESS");

        CompleteProposalExecute(proposalId, proposal);
    }

    /// <inheritdoc />
    public void RedeemCertificate()
    {
        EnsureNotPayable();
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

        Log(new CreateVaultCertificateLog { Owner = to, Amount = amount, VestedBlock = vestedBlock });
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

    private ulong CreateCertificateTypeProposalExecute(Address recipient, UInt256 amount, string description, byte type)
    {
        var existingProposalId = GetCertificateProposalIdByRecipient(recipient);

        Assert(existingProposalId == 0ul, "OPDEX: RECIPIENT_PROPOSAL_IN_PROGRESS");

        var proposalId = CreateProposalExecute(recipient, amount, description, type);

        SetCertificateProposalIdByRecipient(recipient, proposalId);

        return proposalId;
    }

    private ulong CreateMinimumAmountChangeProposalExecute(UInt256 amount, string description, byte type)
    {
        Assert(amount <= ulong.MaxValue, "OPDEX: EXCESSIVE_AMOUNT");
        return CreateProposalExecute(Message.Sender, amount, description, type);
    }

    private ulong CreateProposalExecute(Address wallet, UInt256 amount, string description, byte type)
    {
        const byte status = (byte)ProposalStatus.Pledge;
        var proposalId = NextProposalId;
        var expiration = Block.Number + PledgeDuration;

        NextProposalId += 1;

        SetProposal(proposalId, new ProposalDetails
        {
            Wallet = wallet,
            Amount = amount,
            Type = type,
            Status = status,
            Expiration = expiration
        });

        Log(new CreateVaultProposalLog
        {
            ProposalId = proposalId,
            Wallet = wallet,
            Amount = amount,
            Description = description,
            Type = type,
            Status = status,
            Expiration = expiration
        });

        return proposalId;
    }

    private void CompleteProposalExecute(ulong proposalId, ProposalDetails proposal)
    {
        var proposalType = (ProposalType)proposal.Type;
        var totalVoteAmount = proposal.YesAmount + proposal.NoAmount;
        var approved = (ProposalStatus)proposal.Status == ProposalStatus.Vote && proposal.YesAmount > proposal.NoAmount && totalVoteAmount >= TotalVoteMinimum;

        if (approved)
        {
            if (proposalType == ProposalType.Create) CreateCertificate(proposal.Wallet, proposal.Amount);
            else if (proposalType == ProposalType.Revoke) RevokeCertificate(proposal.Wallet);
            else if (proposalType == ProposalType.TotalPledgeMinimum) TotalPledgeMinimum = (ulong)proposal.Amount;
            else TotalVoteMinimum = (ulong)proposal.Amount;
        }

        proposal.Status = (byte)ProposalStatus.Complete;
        SetProposal(proposalId, proposal);

        if (proposalType == ProposalType.Create || proposalType == ProposalType.Revoke)
        {
            SetCertificateProposalIdByRecipient(proposal.Wallet, 0ul);

            if (proposalType == ProposalType.Create) TotalProposedAmount -= proposal.Amount;
        }

        Log(new CompleteVaultProposalLog { ProposalId = proposalId, Approved = approved });
    }

    private void ValidateProposal(UInt256 amount, string description)
    {
        EnsureNotPayable();
        Assert(!string.IsNullOrWhiteSpace(description) && description.Length <= 200, "OPDEX: INVALID_DESCRIPTION");
        Assert(amount > 0, "OPDEX: INVALID_AMOUNT");
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

    private void EnsureNotPayable() => Assert(Message.Value == 0ul, "OPDEX: NOT_PAYABLE");

    private void EnsureWithdrawalValidity(ulong requestedWithdrawAmount, ulong balance)
    {
        EnsureNotPayable();
        Assert(requestedWithdrawAmount > 0ul, "OPDEX: INSUFFICIENT_WITHDRAW_AMOUNT");
        Assert(requestedWithdrawAmount <= balance, "OPDEX: INSUFFICIENT_FUNDS");
    }
}
