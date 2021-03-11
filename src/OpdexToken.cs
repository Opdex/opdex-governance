using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

[Deploy]
public class OpdexToken : StandardToken, IStandardToken256
{
    private const ulong BlocksPerYear = 1_971_000; // based on 16 second blocks
    
    // Todo: This shouldn't be hardcoded here
    private readonly UInt256[] _ownerSchedule = { 50_000_000, 75_000_000, 50_000_000, 25_000_000 };
    private readonly UInt256[] _miningSchedule = { 300_000_000, 150_000_000, 100_000_000, 50_000_000 };
    private readonly UInt256[] _advisorSchedule = { 12_500_000, 17_500_000, 12_500_000, 7_500_000 };
    private readonly UInt256[] _investorSchedule = { 37_500_000, 57_500_000, 37_500_000, 17_500_000 };
    
    public OpdexToken(ISmartContractState contractState, string name, string symbol, byte decimals)
        : base(contractState, name, symbol, decimals)
    {
        Owner = Message.Sender;
        Genesis = Block.Number;
        MiningGovernance = CreateMiningGovernanceContract();
        Distribute();
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

    public UInt256 AdvisorSupply
    {
        get => State.GetUInt256(nameof(AdvisorSupply));
        private set => State.SetUInt256(nameof(AdvisorSupply), value);
    }
    
    public UInt256 InvestorSupply
    {
        get => State.GetUInt256(nameof(InvestorSupply));
        private set => State.SetUInt256(nameof(InvestorSupply), value);
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
    
    public bool Distribute()
    {
        var owner = Owner;
        var miningGov = MiningGovernance;
        var yearIndex = YearIndex;
        var block = Block.Number;
        
        if (block < GetBlockForYearIndex(yearIndex)) return false;
        
        if (yearIndex <= 3)
        {
            var advisorTokens = _advisorSchedule[yearIndex];
            var investorTokens = _investorSchedule[yearIndex];
            var miningTokens = _miningSchedule[yearIndex];
            var ownerTokens = _ownerSchedule[yearIndex];
            
            AdvisorSupply += advisorTokens;
            InvestorSupply += investorTokens;
            TotalSupply += advisorTokens + investorTokens + miningTokens + ownerTokens;
            
            SetBalance(owner, GetBalance(owner) + ownerTokens);
            SetBalance(miningGov, GetBalance(miningGov) + miningTokens);
        }
        else
        {
            var inflation = (TotalSupply / 100) * 2;
            
            SetBalance(miningGov, GetBalance(miningGov) + inflation);

            TotalSupply += inflation;
        }
        
        YearIndex++;

        return true;
    }
    
    public void CreateApplication(byte applicationType, UInt256 amount)
    {
        var to = Message.Sender;
        var existingApplication = GetApplication(to);
        
        Assert(existingApplication.To == Address.Zero, "OPDEX: APPLICATION_EXISTS");
        
        SetApplication(new Application
        {
            To = to,
            Type = applicationType,
            Amount = amount
        });
        
        Log(new ApplicationRequestEvent
        {
            To = to,
            Type = applicationType,
            Amount = amount
        });
    }

    public void ReviewApplication(bool approve, Address to)
    {
        // Require Multiple Signatures/Reviewers
        Assert(Message.Sender == Owner, "OPDEX: UNAUTHORIZED_REVIEWER");

        var application = GetApplication(to);
        Assert(application.To == to, "OPDEX: INVALID_RECEIVER");

        if (approve)
        {
            if (application.Type == (byte) ApplicationType.Investor)
            {
                Assert(InvestorSupply >= application.Amount, "OPDEX: INSUFFICIENT_SUPPLY");
                InvestorSupply -= application.Amount;
            }
            else
            {
                Assert(AdvisorSupply >= application.Amount, "OPDEX: INSUFFICIENT_SUPPLY");
                AdvisorSupply -= application.Amount;
            }

            var newBalance = GetBalance(to) + application.Amount;
            SetBalance(to, newBalance);
        }

        ClearApplication(to);

        Log(new ApplicationReviewEvent
        {
            To = application.To,
            Type = application.Type,
            Amount = application.Amount,
            Approved = approve
        });
    }

    private Address CreateMiningGovernanceContract()
    {
        var miner = Create<LiquidityStakingFactory>(0ul, new object[] { Address, Block.Number, Address.Zero });
        
        Assert(miner.Success && miner.NewContractAddress != Address.Zero, "OPDEX: CREATE_MINER_FAILED");
        
        return miner.NewContractAddress;
    }
    
    public struct Application
    {
        public byte Type;
        public UInt256 Amount;
        public Address To;
    }

    public struct ApplicationRequestEvent
    {
        [Index] public Address To;
        public byte Type;
        public UInt256 Amount;
    }
    
    public struct ApplicationReviewEvent
    {
        [Index] public Address To;
        public byte Type;
        public UInt256 Amount;
        public bool Approved;
    }

    public enum ApplicationType : byte
    {
        Investor = 0,
        Advisor = 1
    }
}