using Stratis.SmartContracts;

[Deploy]
public class OpdexToken : StandardToken
{
    private const ulong BlocksPerYear = 1_971_000; // based on 16 second blocks
    
    public OpdexToken(ISmartContractState contractState, string name, string symbol, byte decimals, byte[] ownerDistribution, byte[] miningDistribution) 
        : base(contractState, name, symbol, decimals)
    {
        var ownerSchedule = Serializer.ToArray<UInt256>(ownerDistribution);
        var miningSchedule = Serializer.ToArray<UInt256>(miningDistribution);
        var ownerLength = ownerSchedule.Length;
        var miningLength = miningSchedule.Length;
        
        Assert(ownerLength > 1 && miningLength > 1 && ownerLength == miningLength);

        Owner = Message.Sender;
        Genesis = Block.Number;
        OwnerSchedule = ownerSchedule;
        MiningSchedule = miningSchedule;
        MiningGovernance = Create<MiningGovernance>(0ul, new object[] {Address}).NewContractAddress;
    }

    public Address Owner
    {
        get => State.GetAddress(nameof(Owner));
        private set => State.SetAddress(nameof(Owner), value);
    }
    
    public Address MiningGovernance
    {
        get => State.GetAddress(nameof(MiningGovernance));
        private set => State.SetAddress(nameof(MiningGovernance), value);
    }
    
    public UInt256[] OwnerSchedule
    {
        get => State.GetArray<UInt256>(nameof(OwnerSchedule));
        private set => State.SetArray(nameof(OwnerSchedule), value);
    }

    public UInt256[] MiningSchedule
    {
        get => State.GetArray<UInt256>(nameof(MiningSchedule));
        private set => State.SetArray(nameof(MiningSchedule), value);
    }

    public ulong Genesis
    {
        get => State.GetUInt64(nameof(Genesis));
        private set => State.SetUInt64(nameof(Genesis), value);
    }

    public uint YearIndex
    {
        get => State.GetUInt32(nameof(YearIndex));
        private set => State.SetUInt32(nameof(YearIndex), value);
    }
    
    public void NominateLiquidityPool()
    {
        Assert(State.IsContract(Message.Sender));

        var balance = GetBalance(Message.Sender);

        if (balance == 0) return;
        
        Call(MiningGovernance, 0ul, nameof(NominateLiquidityPool), new object[] {Message.Sender, balance});
    }
    
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
        var notificationResponse = Call(miningGov, 0ul, "NotifyDistribution", new object[] {data});
        
        Assert(notificationResponse.Success, "OPDEX: FAILED_DISTRIBUTION_NOTIFICATION");

        TotalSupply += supplyIncrease;
        YearIndex++;
        
        Log(new DistributionEvent
        {
            OwnerAddress = owner,
            MiningAddress = miningGov,
            OwnerAmount = ownerTokens,
            MiningAmount = miningTokens,
            YearIndex = yearIndex
        });
    }

    public void SetOwner(Address owner)
    {
        Assert(Message.Sender == Owner, "OPDEX: UNAUTHORIZED");
        
        Owner = owner;
        
        Log(new OwnerChangeEvent { From = Message.Sender, To = owner });
    }
}