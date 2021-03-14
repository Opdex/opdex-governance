using Stratis.SmartContracts;

// Todo: Initial setting and enforcement of NominationPeriodEnd
// Todo: Initial deploy restrict first miners for at least a week
// Todo: Nomination Bug
public class LiquidityStakingGovernance : SmartContract
{
    private const uint MaximumNominations = 4;
    private const uint BlocksPerYear = 1_971_000;
    private const uint TwoMonthsBlocks = 328_500; // Blocks pr year / 6
    private const uint ContractsPerYear = 24; // 4 concurrent mining contracts, 2 months each
        
    public LiquidityStakingGovernance(ISmartContractState contractState, Address rewardToken) : base(contractState)
    {
        Genesis = Block.Number;
        RewardToken = rewardToken;
    }

    public Address RewardToken
    {
        get => State.GetAddress(nameof(RewardToken));
        private set => State.SetAddress(nameof(RewardToken), value);
    }

    public ulong Genesis
    {
        get => State.GetUInt64(nameof(Genesis));
        private set => State.SetUInt64(nameof(Genesis), value);
    }

    public ulong NominationPeriodEnd
    {
        get => State.GetUInt64(nameof(NominationPeriodEnd));
        private set => State.SetUInt64(nameof(NominationPeriodEnd), value);
    }

    public uint BucketIndex
    {
        get => State.GetUInt32(nameof(BucketIndex));
        private set => State.SetUInt32(nameof(BucketIndex), value);
    }

    public UInt256 BucketReward
    {
        get => State.GetUInt256(nameof(BucketReward));
        private set => State.SetUInt256(nameof(BucketReward), value);
    }
    
    public Address Controller
    {
        get => State.GetAddress(nameof(Controller));
        private set => State.SetAddress(nameof(Controller), value);
    }

    public Address GetStakingContract(Address stakingToken)
    {
        return State.GetAddress($"StakingContract:{stakingToken}");
    }
    
    private void SetStakingContract(Address stakingToken, Address stakingContract)
    {
        State.SetAddress($"StakingContract:{stakingToken}", stakingContract);
    }

    public Nomination GetNomination(uint rank)
    {
        return State.GetStruct<Nomination>($"Nomination:{rank}");
    }
    
    private void SetNomination(Nomination nomination, uint rank)
    {
        State.SetStruct($"Nomination:{rank}", nomination);
    }
    
    public void SetController(Address newController)
    {
        Assert(Controller == Address.Zero || Message.Sender == Controller, "OPDEX: UNAUTHORIZED");

        Controller = newController;
    }
    
    public void Initialize(byte[] stakingTokens)
    {
        Assert(Message.Sender == RewardToken);

        SetBucketReward();

        var tokens = Serializer.ToArray<Address>(stakingTokens);
        
        Assert(tokens.Length == MaximumNominations);

        foreach (var token in tokens) Deploy(token);
    }

    public void NotifyRewardAmounts()
    {
        Assert(Block.Number > NominationPeriodEnd);

        var reward = BucketReward;
        
        for (var i = 0U; i < MaximumNominations - 1; i++) NotifyRewardAmountExecute(i, reward);

        NominationPeriodEnd += TwoMonthsBlocks;
    }
    
    public void NotifyRewardAmount(uint index)
    {
        Assert(index < MaximumNominations);
        Assert(Block.Number > NominationPeriodEnd);

        NotifyRewardAmountExecute(index, BucketReward);

        var allFunded = true;
        
        for (var i = 0U; i < MaximumNominations - 1; i++)
        {
            var nomination = GetNomination(index);

            if (nomination.Funded) continue;
            
            allFunded = false;
            
            break;
        }

        if (allFunded) NominationPeriodEnd += TwoMonthsBlocks;
    }
    
    public void Nominate(Address stakingToken, UInt256 weight)
    {
        Assert(Message.Sender == Controller);

        if (Block.Number <= NominationPeriodEnd) return;

        const uint maxNominationIndex = MaximumNominations - 1;

        for (var i = 0U; i < maxNominationIndex; i++)
        {
            var nomination = GetNomination(i);

            // Todo: Bug, if someone votes twice with the same weight it'll include duplicate nominations, need to also check the address 
            if (weight <= nomination.Weight) continue;
            
            var stakingAddress = Deploy(stakingToken);
            
            var newNomination = new Nomination { Weight = weight, StakingAddress = stakingAddress };

            for (var n = maxNominationIndex; n > i; n--) SetNomination(GetNomination(n - 1), n);
            
            SetNomination(newNomination, i);
            
            Log(new NominationEvent { StakingToken = stakingToken, Weight = weight, StakingAddress = stakingAddress }); 

            break;
        }
    }
    
    private Address Deploy(Address stakingToken)
    {
        var stakingContract = GetStakingContract(stakingToken);

        if (stakingContract != Address.Zero) return stakingContract;

        stakingContract = Create<LiquidityStaking>(0ul, new object[] { Address, RewardToken, stakingToken }).NewContractAddress;
        
        SetStakingContract(stakingToken, stakingContract);
        
        Log(new LiquidityStakingDeploymentEvent { Address = stakingContract });

        return stakingContract;
    }

    private void SetBucketReward()
    {
        var bucketIndex = BucketIndex;

        if (bucketIndex > 0) BucketIndex = 0;
        
        var factoryBalance = (UInt256)Call(RewardToken, 0ul, "GetBalance", new object[] {Address}).ReturnValue;
        
        Assert(factoryBalance > UInt256.Zero, "INVALID_BALANCE");

        var bucketReward = factoryBalance / ContractsPerYear;
        
        BucketReward = bucketReward;
    }

    private void NotifyRewardAmountExecute(uint index, UInt256 reward)
    {
        var nomination = GetNomination(index);
        
        if (nomination.Funded) return;
        
        SafeTransferTo(RewardToken, nomination.StakingAddress, reward);

        Assert(Call(nomination.StakingAddress, 0, "NotifyRewardAmount").Success);
        
        nomination.Funded = true;
        
        SetNomination(nomination, index);

        if (BucketIndex++ == MaximumNominations) SetBucketReward();
    }

    private void SafeTransferTo(Address token, Address to, UInt256 amount)
    {
        if (amount == 0) return;
        
        var result = Call(token, 0, "TransferTo", new object[] {to, amount});
        
        Assert(result.Success && (bool)result.ReturnValue, "OPDEX: INVALID_TRANSFER_TO");
    }

    public struct LiquidityStakingDeploymentEvent
    {
        [Index] public Address Address;
    }

    public struct Nomination
    {
        public Address StakingAddress;
        public UInt256 Weight;
        public bool Funded;
    }

    public struct NominationEvent
    {
        [Index] public Address StakingToken;
        [Index] public Address StakingAddress;
        public UInt256 Weight;
    }
}