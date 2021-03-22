using Stratis.SmartContracts;

// Todo: Initial setting and enforcement of NominationPeriodEnd
// Todo: Initial deploy restrict first miners for at least a week
public class MiningGovernance : BaseContract
{
    private const uint MaximumNominations = 3;
    private const uint BlocksPerYear = 1_971_000;
    private const uint OneMonthBlocks = 164_250;
    private const uint BucketsPerYear = 48;
        
    public MiningGovernance(ISmartContractState contractState, Address rewardToken) : base(contractState)
    {
        Genesis = Block.Number;
        RewardToken = rewardToken;
        NominationPeriodEnd = Block.Number + 40_000; // Todo: One Week
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

    public bool Locked
    {
        get => State.GetBool(nameof(Locked));
        private set => State.SetBool(nameof(Locked), value);
    }

    public Nomination FixedNomination
    {
        get => State.GetStruct<Nomination>(nameof(FixedNomination));
        private set => State.SetStruct(nameof(FixedNomination), value);
    }

    public Nomination[] Nominations
    {
        get => State.GetArray<Nomination>(nameof(Nominations));
        private set => State.SetArray(nameof(Nominations), value);
    }

    public Address GetMiningPool(Address stakingToken) => State.GetAddress($"MiningPool:{stakingToken}");

    private void SetMiningPool(Address stakingToken, Address miningPool) => State.SetAddress($"MiningPool:{stakingToken}", miningPool);
    
    // Todo: I don't like this at all...
    // 1 call - 4 deploys
    public void Initialize(byte[] stakingTokens)
    {
        EnsureUnlocked();
        Assert(FixedNomination.StakingToken == Address.Zero);

        SetBucketReward();

        var tokens = Serializer.ToArray<Address>(stakingTokens);
        
        Assert(tokens.Length == MaximumNominations + 1);
        
        var nominations = new Nomination[MaximumNominations];
        
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            var nomination = new Nomination {StakingToken = token};
            
            if (i == 0) FixedNomination = nomination;
            else nominations[i] = nomination;
            
            Deploy(token);
        }

        Nominations = nominations;
        
        Unlock();
    }

    public void NotifyRewardAmounts()
    {
        EnsureUnlocked();
        Assert(Block.Number > NominationPeriodEnd);

        var reward = BucketReward;
        
        for (var i = 0U; i < MaximumNominations - 1; i++) NotifyRewardAmountExecute(i, reward);

        NominationPeriodEnd += OneMonthBlocks;
        
        Unlock();
    }
    
    public void NotifyRewardAmount(uint index)
    {
        EnsureUnlocked();
        Assert(index < MaximumNominations);
        Assert(Block.Number > NominationPeriodEnd);

        NotifyRewardAmountExecute(index, BucketReward);

        var allFunded = true;
        
        for (var i = 0U; i < MaximumNominations - 1; i++)
        {
            var nomination = Nominations[i];

            if (nomination.Funded) continue;
            
            allFunded = false;
            
            break;
        }

        if (allFunded) NominationPeriodEnd += OneMonthBlocks;
        
        Unlock();
    }
    
    private void NotifyRewardAmountExecute(uint index, UInt256 reward)
    {
        var nomination = Nominations[index];
        
        if (nomination.Funded) return;
        
        SafeTransferTo(RewardToken, nomination.StakingToken, reward);

        Assert(Call(nomination.StakingToken, 0, "NotifyRewardAmount").Success);
        
        nomination.Funded = true;

        Nominations[index] = nomination;

        var bucketIndex = BucketIndex;
        
        if (++bucketIndex == BucketsPerYear)
        {
            BucketIndex = 0;
            SetBucketReward();
        }
        else
        {
            BucketIndex = bucketIndex;
        }
    }

    // Refactor again
    // Assume that there can be no nominations, just insert.
    // If filled nominations - find the lowest weighted one and compare
    // If more weight - persist over previous nomination
    // else ignore
    public void Nominate(Address stakingToken, UInt256 weight)
    {
        var validSender = Message.Sender == RewardToken;
        var validStakingToken = State.IsContract(stakingToken);
        var validNominationPeriod = Block.Number <= NominationPeriodEnd;
        
        if (!validSender || !validStakingToken || !validNominationPeriod) return;
        
        var nomination = new Nomination {StakingToken = stakingToken, Weight = weight};
        var nominations = Nominations;

        if (TryUpdateNomination(nominations, nomination)) return;
        
        TrySetNomination(nominations, nomination);
    }

    private bool TryUpdateNomination(Nomination[] nominations, Nomination nomination)
    {
        var isCurrentlyNominated = false;
        
        for (var i = 0; i < nominations.Length; i++)
        {
            if (nominations[i].StakingToken != nomination.StakingToken) continue;
            
            isCurrentlyNominated = true;
            nominations[i] = nomination;
            Nominations = OrderNominationsByWeight(nominations);
            Log(new NominationEvent
            {
                StakingToken = nomination.StakingToken, 
                Weight = nomination.Weight, 
                MiningPool = GetMiningPool(nomination.StakingToken)
            });
            break;
        }

        return isCurrentlyNominated;
    }

    private void TrySetNomination(Nomination[] nominations, Nomination nomination)
    {
        for (var i = 0; i < nominations.Length; i++)
        {
            var isLessThan = nomination.Weight < nominations[i].Weight;

            if (isLessThan) continue;
            
            var isEqualButNotLast = nomination.Weight == nominations[i].Weight && i != nominations.Length - 1;

            if (isEqualButNotLast) continue;

            var isLast = i == nominations.Length - 1;
            if (!isLast)
            {
                for (var n = nominations.Length - 1; n > i; n--)
                    nominations[n] = nominations[n - 1];
            }
            
            nominations[i] = nomination;
            Nominations = nominations;
            
            Log(new NominationEvent
            {
                StakingToken = nomination.StakingToken, 
                Weight = nomination.Weight, 
                MiningPool = Deploy(nomination.StakingToken)
            });

            break;
        }
    }

    private uint LowestNominationWeightIndex(Nomination[] nominations)
    {
        UInt256 lowestWeight = 0;
        uint lowestIndex = 0;

        for (uint i = 0; i < nominations.Length; i++)
        {
            var weight = nominations[i].Weight;

            if (i == 0)
            {
                lowestWeight = weight;
                continue;
            }

            if (weight > lowestWeight) continue;

            lowestWeight = weight;
            lowestIndex = i;
        }

        return lowestIndex;
    }

    private static Nomination[] OrderNominationsByWeight(Nomination[] nominations)
    {
        for (var i = 0; i <= nominations.Length - 1; i++)  
        {  
            for (var j = i + 1; j < nominations.Length; j++)
            {
                if (nominations[i].Weight <= nominations[j].Weight) continue;
                
                var tempNomination = nominations[i];  
                nominations[i] = nominations[j];  
                nominations[j] = tempNomination;
            }  
        }

        return nominations;
    }
    
    private Address Deploy(Address stakingToken)
    {
        var miningPool = GetMiningPool(stakingToken);

        if (miningPool != Address.Zero) return miningPool;

        miningPool = Create<Mining>(0ul, new object[] { Address, RewardToken, stakingToken }).NewContractAddress;
        
        SetMiningPool(stakingToken, miningPool);
        
        Log(new MiningPoolCreatedEvent { MiningPool = miningPool, StakingPool = stakingToken });

        return miningPool;
    }

    // Todo: Maybe this should have a public outlet to prevent lockup
    // If so, need another assert or BucketIndex == 0 could run unlimited times
    private void SetBucketReward()
    {
        Assert(BucketIndex == 0);
        
        var balance = (UInt256)Call(RewardToken, 0ul, "GetBalance", new object[] {Address}).ReturnValue;
        
        Assert(balance > BucketsPerYear, "INVALID_BALANCE");

        BucketReward = balance / BucketsPerYear;
    }

    private void SafeTransferTo(Address token, Address to, UInt256 amount)
    {
        if (amount == 0) return;
        
        var result = Call(token, 0, "TransferTo", new object[] {to, amount});
        
        Assert(result.Success && (bool)result.ReturnValue, "OPDEX: INVALID_TRANSFER_TO");
    }

    private void EnsureUnlocked()
    {
        Assert(!Locked, "OPDEX: LOCKED");
        Locked = true;
    }

    private void Unlock() => Locked = false;
}