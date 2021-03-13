﻿using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

[Deploy]
public class OpdexToken : StandardToken, IStandardToken256
{
    private const ulong BlocksPerYear = 1_971_000; // based on 16 second blocks
    
    // Todo: This shouldn't be hardcoded here
    private readonly UInt256[] _ownerSchedule = { 50_000_000, 75_000_000, 50_000_000, 25_000_000 };
    private readonly UInt256[] _miningSchedule = { 300_000_000, 150_000_000, 100_000_000, 50_000_000 };
    private readonly UInt256[] _treasurySchedule = { 50_000_000, 75_000_000, 50_000_000, 25_000_000 };
    
    public OpdexToken(ISmartContractState contractState, string name, string symbol, byte decimals)
        : base(contractState, name, symbol, decimals)
    {
        Owner = Message.Sender;
        Genesis = Block.Number;
        MiningGovernance = Create<LiquidityStakingGovernance>(0ul, new object [] { Address }).NewContractAddress;
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

    public ulong Genesis
    {
        get => State.GetUInt64(nameof(Genesis));
        private set => State.SetUInt64(nameof(Genesis), value);
    }
    
    public UInt256 TreasurySupply
    {
        get => State.GetUInt256(nameof(TreasurySupply));
        private set => State.SetUInt256(nameof(TreasurySupply), value);
    }

    public uint YearIndex
    {
        get => State.GetUInt32(nameof(YearIndex));
        private set => State.SetUInt32(nameof(YearIndex), value);
    }

    public Application GetApplication(Address to)
    {
        return State.GetStruct<Application>($"Application:{to}");
    }

    private void SetApplication(Application application)
    {
        State.SetStruct($"Application:{application.To}", application);
    }

    private void ClearApplication(Address to)
    {
        State.Clear($"Application:{to}");
    }

    private ulong GetBlockForYearIndex(uint index)
    {
        var genesis = Genesis;
        
        if (index == 0) return genesis;
        
        return BlocksPerYear * index + genesis;
    }
    
    public bool Distribute(byte[] stakingTokens)
    {
        var miningGov = MiningGovernance;
        Assert(miningGov != Address.Zero);
        
        var owner = Owner;
        var yearIndex = YearIndex;

        if (Block.Number < GetBlockForYearIndex(yearIndex)) return false;
        
        if (yearIndex <= 3)
        {
            var treasuryTokens = _treasurySchedule[yearIndex];
            var miningTokens = _miningSchedule[yearIndex];
            var ownerTokens = _ownerSchedule[yearIndex];
            
            // Todo: Create Treasury Contract
            TreasurySupply += treasuryTokens;
            TotalSupply += treasuryTokens + miningTokens + ownerTokens;
            
            SetBalance(owner, GetBalance(owner) + ownerTokens);
            SetBalance(miningGov, GetBalance(miningGov) + miningTokens);

            if (yearIndex == 0)
            {
                Call(MiningGovernance, 0ul, "Initialize", new object[] {stakingTokens});
            }
        }
        else
        {
            var inflation = TotalSupply / 100 * 2;
            var twentyPercentInflation = inflation / 100 * 20;
            var treasuryInflation = twentyPercentInflation;
            var ownerInflation = twentyPercentInflation;
            var miningInflation = inflation - (treasuryInflation + ownerInflation);
            
            SetBalance(miningGov, GetBalance(miningGov) + miningInflation);
            SetBalance(owner, GetBalance(owner) + ownerInflation);

            TreasurySupply += treasuryInflation;
            TotalSupply += inflation;
        }
        
        YearIndex++;

        return true;
    }
    
    public void CreateApplication(byte applicationType, UInt256 amount, Address to)
    {
        var existingApplication = GetApplication(to);
        
        Assert(existingApplication.To == Address.Zero, "OPDEX: APPLICATION_EXISTS");
        
        SetApplication(new Application
        {
            From = Message.Sender,
            To = to,
            Type = applicationType,
            Amount = amount
        });
        
        Log(new ApplicationRequestEvent
        {
            From = Message.Sender,
            To = to,
            Type = applicationType,
            Amount = amount
        });
    }

    public void ReviewApplication(bool approval, Address to)
    {
        // Require Multiple Signatures/Reviewers
        Assert(Message.Sender == Owner, "OPDEX: UNAUTHORIZED_REVIEWER");

        var application = GetApplication(to);
        Assert(application.To != Address.Zero, "OPDEX: INVALID_RECEIVER");

        if (approval)
        {
            Assert(TreasurySupply >= application.Amount, "OPDEX: INSUFFICIENT_SUPPLY");
            TreasurySupply -= application.Amount;

            var newBalance = GetBalance(to) + application.Amount;
            SetBalance(to, newBalance);
        }

        ClearApplication(to);

        Log(new ApplicationReviewEvent
        {
            From = application.From,
            To = application.To,
            Type = application.Type,
            Amount = application.Amount,
            Approved = approval
        });
    }
    
    public struct Application
    {
        public Address From;
        public byte Type;
        public UInt256 Amount;
        public Address To;
    }

    public struct ApplicationRequestEvent
    {
        [Index] public Address From;
        [Index] public Address To;
        public byte Type;
        public UInt256 Amount;
    }
    
    public struct ApplicationReviewEvent
    {
        [Index] public Address From;
        [Index] public Address To;
        public byte Type;
        public UInt256 Amount;
        public bool Approved;
    }

    public enum ApplicationType : byte
    {
        Investor = 0,
        Advisor = 1,
        Miner = 2,
        Other = 3
    }
}