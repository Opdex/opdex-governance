using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

[Deploy]
public class OpdexToken : StandardToken, IStandardToken256
{
    private const ulong BlocksPerYear = 1_971_000; // based on 16 second blocks
    
    public OpdexToken(ISmartContractState contractState, string name, string symbol, byte decimals, byte[] ownerDistribution, byte[] miningDistribution) 
        : base(contractState, name, symbol, decimals)
    {
        var ownerSchedule = Serializer.ToArray<UInt256>(ownerDistribution);
        var miningSchedule = Serializer.ToArray<UInt256>(miningDistribution);
        var ownerLength = (uint)ownerSchedule.Length;
        var miningLength = (uint)miningSchedule.Length;
        
        Assert(ownerLength > 1 &&  miningLength > 1 && ownerLength == miningLength);

        Owner = Message.Sender;
        Genesis = Block.Number;
        OwnerSchedule = ownerSchedule;
        MiningSchedule = miningSchedule;
        InflationIndex = ownerLength - 1;
    }

    public Address Owner
    {
        get => State.GetAddress(nameof(Owner));
        private set => State.SetAddress(nameof(Owner), value);
    }
    
    public UInt256[] OwnerSchedule
    {
        get => State.GetArray<UInt256>(nameof(OwnerSchedule));
        private set => State.SetArray(nameof(OwnerSchedule), value);
    }
    
    public Address MiningGovernance
    {
        get => State.GetAddress(nameof(MiningGovernance));
        private set => State.SetAddress(nameof(MiningGovernance), value);
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
    
    public uint InflationIndex
    {
        get => State.GetUInt32(nameof(InflationIndex));
        private set => State.SetUInt32(nameof(InflationIndex), value);
    }

    public uint YearIndex
    {
        get => State.GetUInt32(nameof(YearIndex));
        private set => State.SetUInt32(nameof(YearIndex), value);
    }

    private ulong GetBlockForYearIndex(uint index)
    {
        var genesis = Genesis;
        
        if (index == 0) return genesis;
        
        return (BlocksPerYear * index) + genesis;
    }
    
    public void Distribute(byte[] stakingTokens)
    {
        var yearIndex = YearIndex;
        var miningGov = MiningGovernance;
        var owner = Owner;
        var ownerSchedule = OwnerSchedule;
        var miningSchedule = MiningSchedule;
        var inflationIndex = InflationIndex;

        Assert(Block.Number >= GetBlockForYearIndex(yearIndex), "TOO_EARLY");

        if (miningGov == Address.Zero)
        {
            miningGov = Create<MiningGovernance>(0ul, new object [] { Address }).NewContractAddress;
            MiningGovernance = miningGov;
        }

        var scheduleIndex = yearIndex < inflationIndex ? yearIndex : inflationIndex;
        var ownerTokens = ownerSchedule[scheduleIndex];
        var miningTokens = miningSchedule[scheduleIndex];
        var supplyIncrease = miningTokens + ownerTokens;
        
        SetBalance(owner, GetBalance(owner) + ownerTokens);
        SetBalance(miningGov, GetBalance(miningGov) + miningTokens);

        if (yearIndex == 0)
        {
            Assert(Call(MiningGovernance, 0ul, "Initialize", new object[] {stakingTokens}).Success);
        }
        
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
        Assert(Message.Sender == Owner, "UNAUTHORIZED");
        
        Owner = owner;
        
        Log(new OwnerChangeEvent { From = Message.Sender, To = owner });
    }

    public struct DistributionEvent
    {
        [Index] public Address OwnerAddress;
        [Index] public Address MiningAddress;
        public UInt256 OwnerAmount;
        public UInt256 MiningAmount;
        public uint YearIndex;
    }

    public struct OwnerChangeEvent
    {
        [Index] public Address From;
        [Index] public Address To;
    }
}