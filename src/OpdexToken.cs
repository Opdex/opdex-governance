using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

public class OpdexToken : SmartContract, IStandardToken256
{
    private const ulong BlocksPerYear = 1_971_000; // based on 16 second blocks
    private UInt256[] OpdexSchedule = { 50_000_000, 75_000_000, 50_000_000, 25_000_000 };
    private UInt256[] MiningSchedule = { 300_000_000, 150_000_000, 100_000_000, 50_000_000 };
    private UInt256[] AdvisorSchedule = { 12_500_000, 17_500_000, 12_500_000, 7_500_000 };
    private UInt256[] InvestorSchedule = { 37_500_000, 57_500_000, 37_500_000, 17_500_000 };
    
    public OpdexToken(ISmartContractState smartContractState, UInt256 totalSupply, string name, string symbol, byte decimals)
        : base(smartContractState)
    {
        Opdex = Message.Sender;
        Name = name;
        Symbol = symbol;
        Decimals = decimals;
        Genesis = Block.Number;
        DistributionSchedule = new []
        {
            Block.Number, 
            GetBlockForYearIndex(1), 
            GetBlockForYearIndex(2), 
            GetBlockForYearIndex(3)
        };
        Distribute();
    }

    public Address Opdex
    {
        get => State.GetAddress(nameof(Opdex));
        private set => State.SetAddress(nameof(Opdex), value);
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

    public UInt256 MiningSupply
    {
        get => State.GetUInt256(nameof(MiningSupply));
        private set => State.SetUInt256(nameof(MiningSupply), value);
    }

    public ulong[] DistributionSchedule
    {
        get => State.GetArray<ulong>(nameof(DistributionSchedule));
        private set => State.SetArray(nameof(DistributionSchedule), value);
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
        var blocksToAdd = BlocksPerYear * (index + 1);
        return Genesis + blocksToAdd;
    }
    
    public bool Distribute()
    {
        var opdex = Opdex;
        var yearIndex = YearIndex;
        var block = Block.Number;
        
        if (yearIndex <= 3)
        {
            if (block < DistributionSchedule[yearIndex])
            {
                return false;
            }
            
            AdvisorSupply += AdvisorSchedule[yearIndex];
            InvestorSupply += InvestorSchedule[yearIndex];
            MiningSupply += MiningSchedule[yearIndex];
        
            SetBalance(opdex, GetBalance(opdex) + OpdexSchedule[yearIndex]);
        
            TotalSupply += AdvisorSchedule[yearIndex] + 
                           InvestorSchedule[yearIndex] + 
                           MiningSchedule[yearIndex] +
                           OpdexSchedule[yearIndex];
        }
        else
        {
            if (block < GetBlockForYearIndex(yearIndex))
            {
                return false;
            }

            var inflation = (TotalSupply / 100) * 2;
            
            MiningSupply += inflation;
            TotalSupply += inflation;
        }
        
        YearIndex++;

        return true;
    }

    private void CreateMiningContract(Address pair, UInt256 amount, ulong duration)
    {
        var miner = Create<LiquidityStakingFactory>(0ul, new object[] { pair, amount, duration });
        Assert(miner.Success, "OPDEX: CREATE_MINER_FAILED");
        
        var minerAddress = miner.NewContractAddress;
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

    // Require 2 Signatures
    public void ReviewApplication(bool approve, Address to)
    {
        Assert(Message.Sender == Opdex, "OPDEX: UNAUTHORIZED_REVIEWER");

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
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    
    #region StandardToken

    public string Symbol
    {
        get => State.GetString(nameof(this.Symbol));
        private set => State.SetString(nameof(this.Symbol), value);
    }

    public string Name
    {
        get => State.GetString(nameof(this.Name));
        private set => State.SetString(nameof(this.Name), value);
    }

    /// <inheritdoc />
    public byte Decimals
    {
        get => State.GetBytes(nameof(this.Decimals))[0];
        private set => State.SetBytes(nameof(this.Decimals), new[] { value });
    }

    /// <inheritdoc />
    public UInt256 TotalSupply
    {
        get => State.GetUInt256(nameof(this.TotalSupply));
        private set => State.SetUInt256(nameof(this.TotalSupply), value);
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

    /// <inheritdoc />
    public bool TransferTo(Address to, UInt256 amount)
    {
        if (amount == 0)
        {
            Log(new TransferLog { From = Message.Sender, To = to, Amount = 0 });

            return true;
        }

        UInt256 senderBalance = GetBalance(Message.Sender);

        if (senderBalance < amount)
        {
            return false;
        }

        SetBalance(Message.Sender, senderBalance - amount);

        SetBalance(to, checked(GetBalance(to) + amount));

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

        UInt256 senderAllowance = Allowance(from, Message.Sender);
        UInt256 fromBalance = GetBalance(from);

        if (senderAllowance < amount || fromBalance < amount)
        {
            return false;
        }

        SetApproval(from, Message.Sender, senderAllowance - amount);

        SetBalance(from, fromBalance - amount);

        SetBalance(to, checked(GetBalance(to) + amount));

        Log(new TransferLog { From = from, To = to, Amount = amount });

        return true;
    }

    /// <inheritdoc />
    public bool Approve(Address spender, UInt256 currentAmount, UInt256 amount)
    {
        if (Allowance(Message.Sender, spender) != currentAmount)
        {
            return false;
        }

        SetApproval(Message.Sender, spender, amount);

        Log(new ApprovalLog { Owner = Message.Sender, Spender = spender, Amount = amount, OldAmount = currentAmount });

        return true;
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

    public struct TransferLog
    {
        [Index]
        public Address From;

        [Index]
        public Address To;

        public UInt256 Amount;
    }

    public struct ApprovalLog
    {
        [Index]
        public Address Owner;

        [Index]
        public Address Spender;

        public UInt256 OldAmount;

        public UInt256 Amount;
    }
    
    #endregion
}