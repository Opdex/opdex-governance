using Stratis.SmartContracts;

/// <summary>
/// Mining token contract, used for staking in Opdex liquidity pools. Distributes to owner and mining governance
/// based on a specified duration between distributions.
/// </summary>
[Deploy]
public class OpdexMinedToken : SmartContract, IOpdexMinedToken
{
    /// <summary>
    /// Constructor initializing opdex token contract.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="ownerDistribution">Serialized UInt256 array of owner distribution amounts.</param>
    /// <param name="miningDistribution">Serialized UInt256 array of mining distribution amounts.</param>
    /// <param name="periodDuration">The number of blocks between token distributions.</param>
    public OpdexMinedToken(ISmartContractState state, byte[] ownerDistribution, byte[] miningDistribution, ulong periodDuration) : base(state)
    {
        var vaultSchedule = Serializer.ToArray<UInt256>(ownerDistribution);
        var miningSchedule = Serializer.ToArray<UInt256>(miningDistribution);

        Assert(vaultSchedule.Length > 1 && vaultSchedule.Length == miningSchedule.Length);

        Name = "Opdex";
        Symbol = "ODX";
        Decimals = 8;
        Creator = Message.Sender;
        Genesis = Block.Number;
        VaultSchedule = vaultSchedule;
        MiningSchedule = miningSchedule;
        PeriodDuration = periodDuration;
        
        var miningDuration = periodDuration / 12;
        MiningGovernance = Create<OpdexMiningGovernance>(0, new object[] {Address, miningDuration}).NewContractAddress;
        Vault = Create<OpdexVault>(0, new object[] {Address, Message.Sender}).NewContractAddress;
    }
    
    /// <inheritdoc />
    public string Symbol
    {
        get => State.GetString(nameof(Symbol));
        private set => State.SetString(nameof(Symbol), value);
    }

    /// <inheritdoc />
    public string Name
    {
        get => State.GetString(nameof(Name));
        private set => State.SetString(nameof(Name), value);
    }

    /// <inheritdoc />
    public byte Decimals
    {
        get => State.GetBytes(nameof(Decimals))[0];
        private set => State.SetBytes(nameof(Decimals), new [] {value});
    }
    
    /// <inheritdoc />
    public UInt256 TotalSupply
    {
        get => State.GetUInt256(nameof(TotalSupply));
        private set => State.SetUInt256(nameof(TotalSupply), value);
    }

    /// <inheritdoc />
    public Address Creator
    {
        get => State.GetAddress(nameof(Creator));
        private set => State.SetAddress(nameof(Creator), value);
    }
    
    /// <inheritdoc />
    public Address MiningGovernance
    {
        get => State.GetAddress(nameof(MiningGovernance));
        private set => State.SetAddress(nameof(MiningGovernance), value);
    }
    
    /// <inheritdoc />
    public Address Vault
    {
        get => State.GetAddress(nameof(Vault));
        private set => State.SetAddress(nameof(Vault), value);
    }
    
    /// <inheritdoc />
    public UInt256[] VaultSchedule
    {
        get => State.GetArray<UInt256>(nameof(VaultSchedule));
        private set => State.SetArray(nameof(VaultSchedule), value);
    }

    /// <inheritdoc />
    public UInt256[] MiningSchedule
    {
        get => State.GetArray<UInt256>(nameof(MiningSchedule));
        private set => State.SetArray(nameof(MiningSchedule), value);
    }

    /// <inheritdoc />
    public ulong Genesis
    {
        get => State.GetUInt64(nameof(Genesis));
        private set => State.SetUInt64(nameof(Genesis), value);
    }
    
    /// <inheritdoc />
    public uint PeriodIndex
    {
        get => State.GetUInt32(nameof(PeriodIndex));
        private set => State.SetUInt32(nameof(PeriodIndex), value);
    }
    
    /// <inheritdoc />
    public ulong PeriodDuration
    {
        get => State.GetUInt64(nameof(PeriodDuration));
        private set => State.SetUInt64(nameof(PeriodDuration), value);
    }

    /// <inheritdoc />
    public UInt256 GetBalance(Address address)
    {
        return State.GetUInt256($"Balance:{address}");
    }

    private void SetBalance(Address address, UInt256 value)
    {
        State.SetUInt256($"Balance:{address}", value);
    }

    private void SetApproval(Address owner, Address spender, UInt256 value)
    {
        State.SetUInt256($"Allowance:{owner}:{spender}", value);
    }

    /// <inheritdoc />
    public UInt256 Allowance(Address owner, Address spender)
    {
        return State.GetUInt256($"Allowance:{owner}:{spender}");
    }
    
    /// <inheritdoc />
    public void NominateLiquidityPool()
    {
        Assert(State.IsContract(Message.Sender), "OPDEX: INVALID_SENDER");

        var balance = GetBalance(Message.Sender);

        if (balance == 0) return;
        
        Call(MiningGovernance, 0ul, nameof(NominateLiquidityPool), new object[] {Message.Sender, balance});
    }
    
    /// <inheritdoc />
    public void Distribute(byte[] data)
    {
        var periodIndex = PeriodIndex;
        if (periodIndex == 0) Assert(Message.Sender == Creator);
        
        var miningGov = MiningGovernance;
        var owner = Creator;
        var vault = Vault;
        var vaultSchedule = VaultSchedule;
        var miningSchedule = MiningSchedule;
        var inflationIndex = (uint)vaultSchedule.Length - 1;
        var minBlock = periodIndex == 0 ? Genesis : PeriodDuration * periodIndex + Genesis;
        
        Assert(Block.Number >= minBlock, "OPDEX: DISTRIBUTION_NOT_READY");

        var scheduleIndex = periodIndex < inflationIndex ? periodIndex : inflationIndex;
        var vaultTokens = vaultSchedule[scheduleIndex];
        var miningTokens = miningSchedule[scheduleIndex];
        var supplyIncrease = miningTokens + vaultTokens;
        
        SetBalance(vault, GetBalance(vault) + vaultTokens);
        SetBalance(miningGov, GetBalance(miningGov) + miningTokens);

        data = periodIndex == 0 ? data : new byte[0];
        var governanceNotification = Call(miningGov, 0ul, nameof(IOpdexMiningGovernance.NotifyDistribution), new object[] {data});
        var vaultNotification = Call(vault, 0, nameof(IOpdexVault.NotifyDistribution), new object[] { vaultTokens });
        
        Assert(governanceNotification.Success && vaultNotification.Success, "OPDEX: FAILED_DISTRIBUTION_NOTIFICATION");

        TotalSupply += supplyIncrease;
        PeriodIndex++;
        
        Log(new DistributionLog
        {
            VaultAmount = vaultTokens,
            MiningAmount = miningTokens,
            PeriodIndex = periodIndex
        });
    }
    
    /// <inheritdoc />
    public bool TransferTo(Address to, UInt256 amount)
    {
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
}