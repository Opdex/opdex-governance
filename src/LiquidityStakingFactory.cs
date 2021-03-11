using Stratis.SmartContracts;

/// <summary>
/// Contract used to manage and deploy liquidity mining contracts for the OPDX token.
/// - Manages total balance of tokens available for mining
/// - Manages Controller address to communicate with and verify Opdex Pairs
/// - Manges Deployment and Verification of 4 maximum concurrent liquidity mining contracts
///
/// - Handle division properly of yearly distributed tokens
/// - Use buckets - the same bucket for 4 contracts 6 times
/// - (BucketIndex = 0, this Balance = 2400 OPDX, Until bucket Index = 23 (24 total contracts, 6 periods per year, 4 at a time)
/// - 100 OPDX are given to each of the 24 contracts. Once the BucketIndex > 23, reset to 0, get this contracts balance and reset)
/// </summary>
public class LiquidityStakingFactory : SmartContract
{
    private const uint MaximumConcurrentContracts = 4;
    private const uint BlocksPerYear = 1_971_000;
    private const uint TwoMonthsBlocks = 328_500; // Blocks pr year / 6
    private const uint OneWeekBlocks = 41_062; // 2 months divided by 8
    private const uint ContractsPerYear = 24; // 4 concurrent sc * 6 periods yr
    private const uint PeriodsPerYear = 6; // 2 months
        
    public LiquidityStakingFactory(ISmartContractState contractState, Address rewardToken, ulong genesis, Address controller) : base(contractState)
    {
        Assert(genesis >= Block.Number, "OPDEX: GENESIS_TOO_SOON");

        RewardToken = rewardToken;
        Genesis = Block.Number + OneWeekBlocks; // When token is minted, one week to activate mining
        Owner = Message.Sender;
        Controller = controller;
        
        // Todo: Should deploy 4 initial mining contracts taken in from byte[] in the constructor
        // The genesis block for mining in those mining contracts would be one week out
    }

    // public Address[] StakingTokens
    // {
    //     get => State.GetArray<Address>(nameof(StakingTokens));
    //     private set => State.SetArray(nameof(StakingTokens), value);
    // }

    public Address RewardToken
    {
        get => State.GetAddress(nameof(RewardToken));
        private set => State.SetAddress(nameof(RewardToken), value);
    }

    public Address Owner
    {
        get => State.GetAddress(nameof(Owner));
        private set => State.SetAddress(nameof(Owner), value); 
    }

    public ulong Genesis
    {
        get => State.GetUInt64(nameof(Genesis));
        private set => State.SetUInt64(nameof(Genesis), value);
    }

    public uint BucketIndex
    {
        get => State.GetUInt32(nameof(BucketIndex));
        private set => State.SetUInt32(nameof(BucketIndex), value);
    }

    public bool NominationEnabled
    {
        get => State.GetBool(nameof(NominationEnabled));
        private set => State.SetBool(nameof(NominationEnabled), value);
    }
    
    public bool DeploymentsAvailable
    {
        get => State.GetBool(nameof(DeploymentsAvailable));
        private set => State.SetBool(nameof(DeploymentsAvailable), value);
    }

    public ulong NominationEndBlock
    {
        get => State.GetUInt64(nameof(NominationEndBlock));
        private set => State.SetUInt64(nameof(NominationEndBlock), value);
    }

    public Nomination[] Nominations
    {
        get => State.GetArray<Nomination>(nameof(Nominations));
        private set => State.SetArray(nameof(Nominations), value);
    }
    
    public Address Controller
    {
        get => State.GetAddress(nameof(Controller));
        private set => State.SetAddress(nameof(Controller), value);
    }

    public StakingRewardsInfo GetStakingRewardsInfo(Address stakingToken)
    {
        return State.GetStruct<StakingRewardsInfo>($"StakingRewardsInfo:{stakingToken}");
    }

    public void SetStakingRewardsInfo(Address stakingToken, StakingRewardsInfo info)
    {
        State.SetStruct($"StakingRewardsInfo:{stakingToken}", info);
    }

    public void Deploy(Address stakingToken, UInt256 rewardAmount)
    {
        Assert(Message.Sender == Owner);
        var info = GetStakingRewardsInfo(stakingToken);
        Assert(info.StakingAddress == Address.Zero, "Already Deployed");

        var liquidityStakingContract = Create<LiquidityStaking>(0ul, new object[] { Address, RewardToken, stakingToken });
        
        info.StakingAddress = liquidityStakingContract.NewContractAddress;
        info.RewardAmount = rewardAmount;
        SetStakingRewardsInfo(info.StakingAddress, info);
        
        // Would add to array of tokens
    }
    
    // Todo: NotifyRewardAmounts method - Loop all available tokens

    public void NotifyRewardAmount(Address stakingToken)
    {
        Assert(Block.Number >= Genesis, "Reward amount not ready");

        var info = GetStakingRewardsInfo(stakingToken);
        Assert(info.StakingAddress != Address.Zero, "Not Deployed");

        if (info.RewardAmount > 0)
        {
            var rewardAmount = info.RewardAmount;
            info.RewardAmount = 0;
            
            SafeTransferTo(RewardToken, info.StakingAddress, rewardAmount);

            var result = Call(info.StakingAddress, 0, "NotifyRewardAmount");
            Assert(result.Success);
        }
    }

    private void SafeTransferTo(Address token, Address to, UInt256 amount)
    {
        if (amount == 0) return;
        
        var result = Call(token, 0, "TransferTo", new object[] {to, amount});
        
        Assert(result.Success && (bool)result.ReturnValue, "OPDEX: INVALID_TRANSFER_TO");
    }

    public struct StakingRewardsInfo
    {
        public Address StakingAddress;
        public UInt256 RewardAmount;
    }

    public void EnableNomination()
    {
        Assert(!NominationEnabled);
        
        // Todo: Validate that there are no active contracts - 4 calls
        // - maybe can be done by block count without the 4 calls

        var success = true;
        Assert(success, "OPDEX: NOMINATION_DISABLED");
        NominationEnabled = true;
    }

    // Todo: Refactor, need a way to deploy safely one by one if gas gets high
    public void DeployNominations()
    {
        Assert(!NominationEnabled && DeploymentsAvailable);
        
        var nominations = Nominations;

        foreach (var nomination in nominations)
        {
            Deploy(nomination.Pair, 0);
        }

        Nominations = new Nomination[4];
        DeploymentsAvailable = false;
    }
    
    public bool Nominate(Address pair)
    {
        Assert(NominationEnabled);

        if (Block.Number > NominationEndBlock)
        {
            NominationEnabled = false;
            DeploymentsAvailable = true;
            return false;
            
            // Deploy or just end it and do nothing?
        }
        
        // Validate through the set Controller that it is an OPDEX pair
        var isPairResponse = Call(Controller, 0, "ValidatePair", new object[] {pair});
        Assert((bool)isPairResponse.ReturnValue, "OPDEX: INVALID_PAIR");
        
        var weightResponse = Call(pair, 0ul, "GetWeight");
        var weight = (UInt256)weightResponse.ReturnValue;
        
        var nominations = Nominations;
        var nominated = false;
        
        for (var i = 0; i < nominations.Length; i++)
        {
            if (weight <= nominations[i].Weight)
            {
                continue;
            }
            
            var nomination = new Nomination {Pair = pair, Weight = weight};

            // Todo: This can be better
            Nominations = i switch
            {
                0 => new[] {nomination, nominations[0], nominations[1], nominations[2]},
                1 => new[] {nominations[0], nomination, nominations[1], nominations[2]},
                2 => new[] {nominations[0], nominations[1], nomination, nominations[2]},
                3 => new[] {nominations[0], nominations[1], nominations[2], nomination},
                _ => Nominations
            };

            nominated = true;

            break;
        }

        if (nominated)
        {
            Log(new NominationEvent { Sender = Message.Sender, Pair = pair, Weight = weight });  
        }
        
        return nominated;
    }

    public void SetController(Address newController)
    {
        Assert(Message.Sender == Controller, "OPDEX: UNAUTHORIZED");

        Controller = newController;
    }

    public struct Nomination
    {
        public Address Pair;
        public UInt256 Weight;
    }

    public struct NominationEvent
    {
        [Index] public Address Sender;
        [Index] public Address Pair;
        public UInt256 Weight;
    }
}