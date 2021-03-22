using System;
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
        var ownerLength = (uint)ownerSchedule.Length;
        var miningLength = (uint)miningSchedule.Length;
        
        Assert(ownerLength > 1 && miningLength > 1 && ownerLength == miningLength);

        Owner = Message.Sender;
        Genesis = Block.Number;
        OwnerSchedule = ownerSchedule;
        MiningSchedule = miningSchedule;
        CreateMiningGovernanceContract();
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

    public uint YearIndex
    {
        get => State.GetUInt32(nameof(YearIndex));
        private set => State.SetUInt32(nameof(YearIndex), value);
    }

    // Todo: Test and consider the scenario where the MiningGovernance address needs to change
    // The change could be for security or new versioning reasons, a change in the middle of 
    // the year would break mining. Current implementation would only work if the change is 
    // done in between yearly distribution periods.
    //
    // Consider, on updating MiningGovernance address, hold a temporary value that gets updated
    // during the yearly called Distribute method. Downside is it still takes a year to implement
    // an updated MiningGovernance contract if ever necessary. Other option is updating immediately
    // and moving funds from old miner to new miner. 
    public void Nominate()
    {
        var nominationParams = new object[] {Message.Sender, GetBalance(Message.Sender)};
        Call(MiningGovernance, 0ul, nameof(Nominate), nominationParams);
    }
    
    public void Distribute(byte[] stakingTokens)
    {
        var yearIndex = YearIndex;
        var miningGov = MiningGovernance;
        var owner = Owner;
        var ownerSchedule = OwnerSchedule;
        var miningSchedule = MiningSchedule;
        var inflationIndex = (uint)ownerSchedule.Length - 1;

        Assert(Block.Number >= GetBlockForYearIndex(yearIndex), "TOO_EARLY");

        var scheduleIndex = yearIndex < inflationIndex ? yearIndex : inflationIndex;
        var ownerTokens = ownerSchedule[scheduleIndex];
        var miningTokens = miningSchedule[scheduleIndex];
        var supplyIncrease = miningTokens + ownerTokens;
        
        SetBalance(owner, GetBalance(owner) + ownerTokens);
        SetBalance(miningGov, GetBalance(miningGov) + miningTokens);

        // Todo: Get this out of here
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
    
    public void SetMiningGovernance(Address miningGovernance)
    {
        Assert(Message.Sender == Owner, "UNAUTHORIZED");
        Assert(State.IsContract(miningGovernance));

        Log(new MiningGovernanceChangeEvent { From = MiningGovernance, To = miningGovernance });
        
        MiningGovernance = miningGovernance;
    }

    private void CreateMiningGovernanceContract()
    {
        var miningGovernance = Create<MiningGovernance>(0ul, new object [] { Address }).NewContractAddress;

        MiningGovernance = miningGovernance;
        
        Log(new MiningGovernanceChangeEvent { From = Address.Zero, To = miningGovernance });
    }

    private ulong GetBlockForYearIndex(uint index)
    {
        var genesis = Genesis;
        
        if (index == 0) return genesis;
        
        return (BlocksPerYear * index) + genesis;
    }
}