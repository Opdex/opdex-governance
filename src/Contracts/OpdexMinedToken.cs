using Stratis.SmartContracts;

/// <summary>
/// Mined token contract distributed through a Mining Governance and Vault smart contract.
/// Used for staking in Opdex liquidity pools to participate in governance for liquidity mining pool selection.
/// </summary>
[Deploy]
public class OpdexMinedToken : SmartContract, IOpdexMinedToken
{
    /// <summary>
    /// Constructor initializing opdex token contract.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="name">The name of the token.</param>
    /// <param name="symbol">The token's ticker symbol.</param>
    /// <param name="vaultDistribution">Serialized UInt256 array of vault distribution amounts.</param>
    /// <param name="miningDistribution">Serialized UInt256 array of mining distribution amounts.</param>
    /// <param name="periodDuration">The number of blocks between token distributions.</param>
    public OpdexMinedToken(ISmartContractState state, string name, string symbol, byte[] vaultDistribution,
                           byte[] miningDistribution, ulong periodDuration) : base(state)
    {
        var vaultSchedule = Serializer.ToArray<UInt256>(vaultDistribution);
        var miningSchedule = Serializer.ToArray<UInt256>(miningDistribution);

        Assert(vaultSchedule.Length > 1 && vaultSchedule.Length == miningSchedule.Length, "OPDEX: INVALID_DISTRIBUTION_SCHEDULE");

        Name = name;
        Symbol = symbol;
        Decimals = 8;
        Creator = Message.Sender;
        VaultSchedule = vaultSchedule;
        MiningSchedule = miningSchedule;
        PeriodDuration = periodDuration;
        MiningGovernance = InitializeMiningGovernance(periodDuration);
        Vault = InitializeVault(periodDuration);
    }

    /// <inheritdoc />
    public string Symbol
    {
        get => State.GetString(TokenStateKeys.Symbol);
        private set => State.SetString(TokenStateKeys.Symbol, value);
    }

    /// <inheritdoc />
    public string Name
    {
        get => State.GetString(TokenStateKeys.Name);
        private set => State.SetString(TokenStateKeys.Name, value);
    }

    /// <inheritdoc />
    public byte Decimals
    {
        get => State.GetBytes(TokenStateKeys.Decimals)[0];
        private set => State.SetBytes(TokenStateKeys.Decimals, new [] {value});
    }

    /// <inheritdoc />
    public UInt256 TotalSupply
    {
        get => State.GetUInt256(TokenStateKeys.TotalSupply);
        private set => State.SetUInt256(TokenStateKeys.TotalSupply, value);
    }

    /// <inheritdoc />
    public Address Creator
    {
        get => State.GetAddress(TokenStateKeys.Creator);
        private set => State.SetAddress(TokenStateKeys.Creator, value);
    }

    /// <inheritdoc />
    public Address MiningGovernance
    {
        get => State.GetAddress(TokenStateKeys.MiningGovernance);
        private set => State.SetAddress(TokenStateKeys.MiningGovernance, value);
    }

    /// <inheritdoc />
    public Address Vault
    {
        get => State.GetAddress(TokenStateKeys.Vault);
        private set => State.SetAddress(TokenStateKeys.Vault, value);
    }

    /// <inheritdoc />
    public UInt256[] VaultSchedule
    {
        get => State.GetArray<UInt256>(TokenStateKeys.VaultSchedule);
        private set => State.SetArray(TokenStateKeys.VaultSchedule, value);
    }

    /// <inheritdoc />
    public UInt256[] MiningSchedule
    {
        get => State.GetArray<UInt256>(TokenStateKeys.MiningSchedule);
        private set => State.SetArray(TokenStateKeys.MiningSchedule, value);
    }

    /// <inheritdoc />
    public ulong Genesis
    {
        get => State.GetUInt64(TokenStateKeys.Genesis);
        private set => State.SetUInt64(TokenStateKeys.Genesis, value);
    }

    /// <inheritdoc />
    public uint PeriodIndex
    {
        get => State.GetUInt32(TokenStateKeys.PeriodIndex);
        private set => State.SetUInt32(TokenStateKeys.PeriodIndex, value);
    }

    /// <inheritdoc />
    public ulong PeriodDuration
    {
        get => State.GetUInt64(TokenStateKeys.PeriodDuration);
        private set => State.SetUInt64(TokenStateKeys.PeriodDuration, value);
    }

    /// <inheritdoc />
    public ulong NextDistributionBlock
    {
        get => State.GetUInt64(TokenStateKeys.NextDistributionBlock);
        private set => State.SetUInt64(TokenStateKeys.NextDistributionBlock, value);
    }

    /// <inheritdoc />
    public UInt256 GetBalance(Address address)
    {
        return State.GetUInt256($"{TokenStateKeys.Balance}:{address}");
    }

    private void SetBalance(Address address, UInt256 value)
    {
        State.SetUInt256($"{TokenStateKeys.Balance}:{address}", value);
    }

    private void SetApproval(Address owner, Address spender, UInt256 value)
    {
        State.SetUInt256($"{TokenStateKeys.Allowance}:{owner}:{spender}", value);
    }

    /// <inheritdoc />
    public UInt256 Allowance(Address owner, Address spender)
    {
        return State.GetUInt256($"{TokenStateKeys.Allowance}:{owner}:{spender}");
    }

    /// <inheritdoc />
    public void NominateLiquidityPool()
    {
        Assert(State.IsContract(Message.Sender), "OPDEX: INVALID_SENDER");

        var balance = GetBalance(Message.Sender);

        if (balance == 0) return;

        // Intentionally ignore the response
        Call(MiningGovernance, 0ul, nameof(IOpdexMiningGovernance.NominateLiquidityPool), new object[] {Message.Sender, balance});
    }

    /// <inheritdoc />
    public void DistributeGenesis(Address firstNomination, Address secondNomination, Address thirdNomination, Address fourthNomination)
    {
        var periodIndex = PeriodIndex;
        Assert(periodIndex == 0, "OPDEX: INVALID_DISTRIBUTION_PERIOD");
        Assert(Message.Sender == Creator, "OPDEX: UNAUTHORIZED");

        var nominations = new [] {firstNomination, secondNomination, thirdNomination, fourthNomination};

        for (var i = 0; i < nominations.Length; i++)
        {
            var nomination = nominations[i];

            Assert(nomination != Address.Zero && State.IsContract(nomination), "OPDEX: INVALID_NOMINATION");

            var next = i + 1;
            while (next < nominations.Length)
            {
                Assert(nomination != nominations[next], "OPDEX: DUPLICATE_NOMINATION");
                next++;
            }
        }

        Genesis = Block.Number;

        DistributeExecute(periodIndex, nominations);
    }

    /// <inheritdoc />
    public void Distribute()
    {
        var periodIndex = PeriodIndex;
        Assert(periodIndex > 0, "OPDEX: INVALID_DISTRIBUTION_PERIOD");

        // 4 Addresses to send
        DistributeExecute(periodIndex, new Address[4]);
    }

    /// <inheritdoc />
    public bool TransferTo(Address to, UInt256 amount)
    {
        if (to == Address.Zero) return false;

        if (amount == 0)
        {
            Log(new TransferLog { From = Message.Sender, To = to, Amount = 0 });
            return true;
        }

        var senderBalance = GetBalance(Message.Sender);

        if (senderBalance < amount) return false;

        SetBalance(Message.Sender, senderBalance - amount);
        SetBalance(to, GetBalance(to) + amount);

        Log(new TransferLog { From = Message.Sender, To = to, Amount = amount });

        return true;
    }

    /// <inheritdoc />
    public bool TransferFrom(Address from, Address to, UInt256 amount)
    {
        if (to == Address.Zero) return false;

        if (amount == 0)
        {
            Log(new TransferLog { From = from, To = to, Amount = 0 });
            return true;
        }

        var senderAllowance = Allowance(from, Message.Sender);
        var fromBalance = GetBalance(from);

        if (senderAllowance < amount || fromBalance < amount) return false;

        SetApproval(from, Message.Sender, senderAllowance - amount);
        SetBalance(from, fromBalance - amount);
        SetBalance(to, GetBalance(to) + amount);

        Log(new TransferLog { From = from, To = to, Amount = amount });

        return true;
    }

    /// <inheritdoc />
    public bool Approve(Address spender, UInt256 currentAmount, UInt256 amount)
    {
        if (Allowance(Message.Sender, spender) != currentAmount) return false;

        SetApproval(Message.Sender, spender, amount);

        Log(new ApprovalLog { Owner = Message.Sender, Spender = spender, Amount = amount, OldAmount = currentAmount });

        return true;
    }

    private static ulong GetNextDistributionBlock(ulong periodDuration, uint periodIndex, ulong genesis)
    {
        return (periodDuration * periodIndex) + genesis;
    }

    private void DistributeExecute(uint periodIndex, Address[] nominations)
    {
        Assert(Block.Number >= NextDistributionBlock, "OPDEX: DISTRIBUTION_NOT_READY");

        var miningGov = MiningGovernance;
        var vault = Vault;
        var miningSchedule = MiningSchedule;
        var vaultSchedule = VaultSchedule;
        var totalSupply = TotalSupply;
        var inflationIndex = (uint)vaultSchedule.Length - 1;
        var scheduleIndex = periodIndex < inflationIndex ? periodIndex : inflationIndex;
        var miningTokens = miningSchedule[scheduleIndex];
        var vaultTokens = vaultSchedule[scheduleIndex];
        var supplyIncrease = miningTokens + vaultTokens;

        if (miningTokens > 0)
        {
            SetBalance(miningGov, GetBalance(miningGov) + miningTokens);

            var nominationParams = new object[] {nominations[0], nominations[1], nominations[2], nominations[3]};
            var governanceNotification = Call(miningGov, 0ul, nameof(IOpdexMiningGovernance.NotifyDistribution), nominationParams);

            Assert(governanceNotification.Success, "OPDEX: FAILED_GOVERNANCE_DISTRIBUTION");
        }

        if (vaultTokens > 0)
        {
            SetBalance(vault, GetBalance(vault) + vaultTokens);

            var vaultNotification = Call(vault, 0, nameof(IOpdexVault.NotifyDistribution), new object[] { vaultTokens });

            Assert(vaultNotification.Success, "OPDEX: FAILED_VAULT_DISTRIBUTION");
        }

        totalSupply += supplyIncrease;
        TotalSupply = totalSupply;

        var nextPeriodIndex = periodIndex + 1;
        PeriodIndex = nextPeriodIndex;

        var nextDistributionBlock = GetNextDistributionBlock(PeriodDuration, nextPeriodIndex, Genesis);
        NextDistributionBlock = nextDistributionBlock;

        Log(new DistributionLog
        {
            MiningAmount = miningTokens,
            VaultAmount = vaultTokens,
            PeriodIndex = periodIndex,
            TotalSupply = totalSupply,
            NextDistributionBlock = nextDistributionBlock
        });
    }

    private Address InitializeMiningGovernance(ulong periodDuration)
    {
        var miningDuration = periodDuration / 12;

        var miningGovernanceResponse = Create<OpdexMiningGovernance>(0, new object[] {Address, miningDuration});

        Assert(miningGovernanceResponse.Success, "OPDEX: INVALID_MINING_GOVERNANCE_ADDRESS");

        return miningGovernanceResponse.NewContractAddress;
    }

    private Address InitializeVault(ulong periodDuration)
    {
        // Todo: Consider 1 year lockup
        // -- Side effects to the Vault contract are commented within should this change be made
        var vestingPeriod = periodDuration * 4;

        var vaultResponse = Create<OpdexVault>(0, new object[] {Address, vestingPeriod});

        Assert(vaultResponse.Success, "OPDEX: INVALID_VAULT_ADDRESS");

        return vaultResponse.NewContractAddress;
    }
}
