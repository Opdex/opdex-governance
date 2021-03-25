using System;
using Stratis.SmartContracts;

public class MiningGovernance : BaseContract, IMiningGovernance
{
    private const uint MaximumNominations = 4;
    private const uint BlocksPerYear = 1_971_000;
    private const uint OneMonthBlocks = BlocksPerYear / 12;
    private const uint BucketsPerYear = 48;
        
    public MiningGovernance(ISmartContractState contractState, Address minedToken) : base(contractState)
    {
        Genesis = Block.Number;
        MinedToken = minedToken;
        NominationPeriodEnd = (OneMonthBlocks / 4) + Block.Number;
    }

    public Address MinedToken
    {
        get => State.GetAddress(nameof(MinedToken));
        private set => State.SetAddress(nameof(MinedToken), value);
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

    public Nomination[] Nominations
    {
        get => State.GetArray<Nomination>(nameof(Nominations));
        private set => State.SetArray(nameof(Nominations), value);
    }

    public Address GetMiningPool(Address stakingToken) 
        => State.GetAddress($"MiningPool:{stakingToken}");

    private void SetMiningPool(Address stakingToken, Address miningPool) 
        => State.SetAddress($"MiningPool:{stakingToken}", miningPool);
    
    public void Initialize(byte[] data)
    {
        EnsureUnlocked();
        Assert(Message.Sender == MinedToken);

        SetBucketRewardExecute();

        var stakingTokens = Serializer.ToArray<Address>(data);
        
        Assert(stakingTokens.Length == MaximumNominations);
        
        var nominations = new Nomination[MaximumNominations];
        
        for (var i = 0; i < stakingTokens.Length; i++)
        {
            nominations[i] = new Nomination {StakingToken = stakingTokens[i]};
            
            Deploy(stakingTokens[i]);
        }

        Nominations = nominations;
        
        Unlock();
    }

    public void NotifyDistribution()
    {
        EnsureUnlocked();
        
        Assert(Message.Sender == MinedToken);
        
        SetBucketRewardExecute();
        
        Unlock();
    }
    
    public void SetBucketReward()
    {
        EnsureUnlocked();
        
        SetBucketRewardExecute();
        
        Unlock();
    }

    public void NotifyRewardAmounts()
    {
        EnsureUnlocked();
        Assert(Block.Number > NominationPeriodEnd);

        var nominations = Nominations;
        
        for (uint i = 0; i < MaximumNominations; i++)
        {
            NotifyRewardAmountExecute(nominations[i], BucketReward);
        }

        ResetNominations();
        
        Unlock();
    }
    
    public void NotifyNextRewardAmount()
    {
        EnsureUnlocked();
        Assert(Block.Number > NominationPeriodEnd);
        
        var nominations = Nominations;
        
        for (uint i = 0; i < MaximumNominations; i++)
        {
            if (nominations[i].StakingToken == Address.Zero) continue;
            
            NotifyRewardAmountExecute(nominations[i], BucketReward);

            if (i == MaximumNominations - 1)
            {
                ResetNominations();
                break;
            }
            
            Nominations[i] = new Nomination();
            break;
        }
        
        Unlock();
    }

    public void Nominate(Address stakingToken, UInt256 weight)
    {
        Assert(Message.Sender == MinedToken, "INVALID_SENDER");
        
        NominateExecute(stakingToken, weight);
    }

    private void NominateExecute(Address stakingToken, UInt256 weight)
    {
        if (Block.Number <= NominationPeriodEnd) return;
        if (weight == 0) return;
        
        var nomination = new Nomination {StakingToken = stakingToken, Weight = weight};
        var nominations = Nominations;
        var numNoms = nominations.Length;
        
        if (numNoms < MaximumNominations)
        {
            Array.Resize(ref nominations, numNoms + 1);           
            nominations[numNoms] = nomination;
        }
        else
        {
            var index = LowestNominationWeightIndex(nominations);
            var lowest = nominations[index];
            if (lowest.Weight >= nomination.Weight) return;
            nominations[index] = nomination;
        }

        Nominations = nominations;
        
        Log(new NominationEvent
        {
            StakingToken = nomination.StakingToken, 
            Weight = nomination.Weight, 
            MiningPool = Deploy(nomination.StakingToken)
        });
    }
    
    private void NotifyRewardAmountExecute(Nomination nomination, UInt256 reward)
    {
        if (nomination.StakingToken == Address.Zero) return;

        var miningPool = GetMiningPool(nomination.StakingToken);
        
        SafeTransferTo(MinedToken, miningPool, reward);

        Assert(Call(miningPool, 0, "NotifyRewardAmount").Success);

        var bucketIndex = BucketIndex;
        
        if (++bucketIndex == BucketsPerYear)
        {
            BucketIndex = 0;
            BucketReward = 0;
            // Todo: will require an active balance at all times...
            SetBucketRewardExecute();
            return;
        }
        
        BucketIndex = bucketIndex;
    }

    private static uint LowestNominationWeightIndex(Nomination[] nominations)
    {
        var lowestIndex = 0u;
        var lowestWeight = nominations[lowestIndex].Weight;

        for (uint i = 1; i < nominations.Length; i++)
        {
            if (nominations[i].Weight > lowestWeight) continue;
            
            lowestWeight = nominations[i].Weight;
            lowestIndex = i;
        }

        return lowestIndex;
    }
    
    private Address Deploy(Address stakingToken)
    {
        var miningPool = GetMiningPool(stakingToken);

        if (miningPool != Address.Zero) return miningPool;

        miningPool = Create<Mining>(0ul, new object[] { Address, MinedToken, stakingToken }).NewContractAddress;
        
        SetMiningPool(stakingToken, miningPool);
        
        Log(new MiningPoolCreatedEvent { MiningPool = miningPool, StakingPool = stakingToken });

        return miningPool;
    }
    
    private void SetBucketRewardExecute()
    {
        Assert(BucketReward == 0);
        
        var balance = (UInt256)Call(MinedToken, 0ul, "GetBalance", new object[] {Address}).ReturnValue;
        
        // Todo: I don't like this, if the balance is > 48 sats this passes, a year of basically no mining lol
        Assert(balance > BucketsPerYear, "INVALID_BALANCE");

        BucketReward = balance / BucketsPerYear;
    }

    private void ResetNominations()
    {
        Nominations = new Nomination[0];
        NominationPeriodEnd += OneMonthBlocks;
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