using Stratis.SmartContracts;

/// <summary>
/// A smart contract that locks tokens for a specified vesting period.
/// </summary>
public class OpdexVault : SmartContract, IOpdexVault
{
    /// <summary>
    /// Constructor initializing an empty vault for locking tokens to be vested.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="token">The locked SRC token.</param>
    /// <param name="vestingDuration">The length in blocks of the vesting period.</param>
    public OpdexVault(ISmartContractState state, Address token, ulong vestingDuration) : base(state)
    {
        Token = token;
        VestingDuration = vestingDuration;
        Governance = InitializeVaultGovernance();
    }

    /// <inheritdoc />
    public ulong Genesis
    {
        get => State.GetUInt64(VaultStateKeys.Genesis);
        private set => State.SetUInt64(VaultStateKeys.Genesis, value);
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
    public Address Governance
    {
        get => State.GetAddress(VaultStateKeys.Governance);
        private set => State.SetAddress(VaultStateKeys.Governance, value);
    }

    /// <inheritdoc />
    public UInt256 TotalSupply
    {
        get => State.GetUInt256(VaultStateKeys.TotalSupply);
        private set => State.SetUInt256(VaultStateKeys.TotalSupply, value);
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
    public void NotifyDistribution(UInt256 amount)
    {
        Assert(Message.Sender == Token, "OPDEX: UNAUTHORIZED");

        TotalSupply += amount;

        if (Genesis == 0) Genesis = Block.Number;
    }

    /// <inheritdoc />
    public void CreateCertificate(Address to, UInt256 amount)
    {
        var governance = Governance;
        var vestingDuration = VestingDuration;

        Assert(Message.Sender == governance, "OPDEX: UNAUTHORIZED");
        Assert(to != governance, "OPDEX: INVALID_CERTIFICATE_HOLDER");
        Assert(amount > 0 && amount <= TotalSupply, "OPDEX: INVALID_AMOUNT");
        // Todo: Change in vesting period will lock the vault after, if changing vesting period, adjust logic below
        Assert(Block.Number < Genesis + vestingDuration, "OPDEX: TOKENS_BURNED");

        var certificate = GetCertificate(to);

        Assert(certificate.Amount == UInt256.Zero, "OPDEX: CERTIFICATE_EXISTS");

        var vestedBlock = Block.Number + vestingDuration;

        certificate = new VaultCertificate { Amount = amount, VestedBlock = vestedBlock, Revoked = false };

        SetCertificate(to, certificate);

        TotalSupply -= amount;

        Log(new CreateVaultCertificateLog{ Owner = to, Amount = amount, VestedBlock = vestedBlock });
    }

    /// <inheritdoc />
    public void RedeemCertificate()
    {
        var certificate = GetCertificate(Message.Sender);

        Assert(certificate.VestedBlock > 0, "OPDEX: CERTIFICATE_NOT_FOUND");
        Assert(certificate.VestedBlock < Block.Number, "OPDEX: CERTIFICATE_VESTING");

        var amountToTransfer = certificate.Amount;

        Log(new RedeemVaultCertificateLog {Owner = Message.Sender, Amount = certificate.Amount, VestedBlock = certificate.VestedBlock});

        SetCertificate(Message.Sender, default(VaultCertificate));

        SafeTransferTo(Token, Message.Sender, amountToTransfer);
    }

    /// <inheritdoc />
    public void RevokeCertificate(Address wallet)
    {
        Assert(Message.Sender == Governance, "OPDEX: UNAUTHORIZED");

        var certificate = GetCertificate(wallet);

        Assert(!certificate.Revoked, "OPDEX: CERTIFICATE_PREVIOUSLY_REVOKED");
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

        TotalSupply += (vestingAmount - newAmount);

        Log(new RevokeVaultCertificateLog {Owner = wallet, OldAmount = vestingAmount, NewAmount = newAmount, VestedBlock = certificate.VestedBlock});

        SetCertificate(wallet, certificate);
    }

    private Address InitializeVaultGovernance()
    {
        var vaultGovernance = Create<OpdexVaultGovernance>(0ul, new object[] { Address });

        Assert(vaultGovernance.Success, "OPDEX: INVALID_VAULT_GOVERNANCE");

        return vaultGovernance.NewContractAddress;
    }

    private void SafeTransferTo(Address token, Address to, UInt256 amount)
    {
        if (amount == 0) return;

        var result = Call(token, 0, nameof(IOpdexMinedToken.TransferTo), new object[] {to, amount});

        Assert(result.Success && (bool)result.ReturnValue, "OPDEX: INVALID_TRANSFER_TO");
    }
}
