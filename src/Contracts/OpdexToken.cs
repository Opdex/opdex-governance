using Stratis.SmartContracts;

/// <summary>
/// 
/// </summary>
[Deploy]
public class OpdexToken : SmartContract, IOpdexToken
{
    private const string TokenName = "Opdex";
    private const string TokenSymbol = "OPDX";
    private const byte TokenDecimals = 8;
    private const ulong BlocksPerYear = 1_971_000; // based on 16 second blocks
    
    /// <summary>
    /// Constructor initializing opdex token contract.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="ownerDistribution"></param>
    /// <param name="miningDistribution"></param>
    public OpdexToken(ISmartContractState state, byte[] ownerDistribution, byte[] miningDistribution) : base(state)
    {
        var ownerSchedule = Serializer.ToArray<UInt256>(ownerDistribution);
        var miningSchedule = Serializer.ToArray<UInt256>(miningDistribution);

        Assert(ownerSchedule.Length > 1 && ownerSchedule.Length == miningSchedule.Length);

        Owner = Message.Sender;
        Genesis = Block.Number;
        OwnerSchedule = ownerSchedule;
        MiningSchedule = miningSchedule;
        MiningGovernance = Create<OpdexMiningGovernance>(0ul, new object[] {Address}).NewContractAddress;
    }
    
    /// <inheritdoc />
    public string Symbol => TokenSymbol;

    /// <inheritdoc />
    public string Name => TokenName;

    /// <inheritdoc />
    public byte Decimals => TokenDecimals;
    
    /// <inheritdoc />
    public UInt256 TotalSupply
    {
        get => State.GetUInt256(nameof(TotalSupply));
        private set => State.SetUInt256(nameof(TotalSupply), value);
    }

    /// <inheritdoc />
    public Address Owner
    {
        get => State.GetAddress(nameof(Owner));
        private set => State.SetAddress(nameof(Owner), value);
    }
    
    /// <inheritdoc />
    public Address MiningGovernance
    {
        get => State.GetAddress(nameof(MiningGovernance));
        private set => State.SetAddress(nameof(MiningGovernance), value);
    }
    
    /// <inheritdoc />
    public UInt256[] OwnerSchedule
    {
        get => State.GetArray<UInt256>(nameof(OwnerSchedule));
        private set => State.SetArray(nameof(OwnerSchedule), value);
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
    public uint YearIndex
    {
        get => State.GetUInt32(nameof(YearIndex));
        private set => State.SetUInt32(nameof(YearIndex), value);
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
        var yearIndex = YearIndex;
        if (yearIndex == 0) Assert(Message.Sender == Owner);
        
        var miningGov = MiningGovernance;
        var owner = Owner;
        var ownerSchedule = OwnerSchedule;
        var miningSchedule = MiningSchedule;
        var inflationIndex = (uint)ownerSchedule.Length - 1;
        var minBlock = yearIndex == 0 ? Genesis : BlocksPerYear * yearIndex + Genesis;
        
        Assert(Block.Number >= minBlock, "OPDEX: DISTRIBUTION_NOT_READY");

        var scheduleIndex = yearIndex < inflationIndex ? yearIndex : inflationIndex;
        var ownerTokens = ownerSchedule[scheduleIndex];
        var miningTokens = miningSchedule[scheduleIndex];
        var supplyIncrease = miningTokens + ownerTokens;
        
        SetBalance(owner, GetBalance(owner) + ownerTokens);
        SetBalance(miningGov, GetBalance(miningGov) + miningTokens);

        data = yearIndex == 0 ? data : new byte[0];
        var notificationResponse = Call(miningGov, 0ul, nameof(IOpdexMiningGovernance.NotifyDistribution), new object[] {data});
        
        Assert(notificationResponse.Success, "OPDEX: FAILED_DISTRIBUTION_NOTIFICATION");

        TotalSupply += supplyIncrease;
        YearIndex++;
        
        Log(new DistributionLog
        {
            OwnerAddress = owner,
            MiningAddress = miningGov,
            OwnerAmount = ownerTokens,
            MiningAmount = miningTokens,
            YearIndex = yearIndex
        });
    }

    /// <inheritdoc />
    public void SetOwner(Address owner)
    {
        Assert(Message.Sender == Owner, "OPDEX: UNAUTHORIZED");
        
        Owner = owner;
        
        Log(new OwnerChangeLog { From = Message.Sender, To = owner });
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