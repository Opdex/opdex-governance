using System;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

/// <summary>
/// Mining governance contract responsible for holding and distributing Opdex tokens to be mined within individual
/// mining pools based on a liquidity pool nomination by Opdex token staking weight. 
/// </summary>
public class OpdexMiningGovernance : SmartContract, IOpdexMiningGovernance
{
    private const uint MaximumNominations = 4;
    private const uint MiningPoolsPerYear = 48;
        
    /// <summary>
    /// Constructor initializing opdex token mining governance contract.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="minedToken">The address of the token being mined.</param>
    /// <param name="periodDuration">The nomination and mining period block duration.</param>
    public OpdexMiningGovernance(ISmartContractState state, Address minedToken, ulong periodDuration) : base(state)
    {
        MinedToken = minedToken;
        PeriodDuration = periodDuration;
        NominationPeriodEnd = periodDuration / 4 + Block.Number;
        Nominations = new Nomination[0];
    }

    /// <inheritdoc />
    public Address MinedToken
    {
        get => State.GetAddress(nameof(MinedToken));
        private set => State.SetAddress(nameof(MinedToken), value);
    }

    /// <inheritdoc />
    public bool Notified
    {
        get => State.GetBool(nameof(Notified));
        private set => State.SetBool(nameof(Notified), value);
    }

    /// <inheritdoc />
    public Nomination[] Nominations
    {
        get => State.GetArray<Nomination>(nameof(Nominations));
        private set => State.SetArray(nameof(Nominations), value);
    }
    
    /// <inheritdoc />
    public ulong PeriodDuration
    {
        get => State.GetUInt64(nameof(PeriodDuration));
        private set => State.SetUInt64(nameof(PeriodDuration), value);
    }

    /// <inheritdoc />
    public ulong NominationPeriodEnd
    {
        get => State.GetUInt64(nameof(NominationPeriodEnd));
        private set => State.SetUInt64(nameof(NominationPeriodEnd), value);
    }

    /// <inheritdoc />
    public uint MiningPoolsFunded
    {
        get => State.GetUInt32(nameof(MiningPoolsFunded));
        private set => State.SetUInt32(nameof(MiningPoolsFunded), value);
    }

    /// <inheritdoc />
    public UInt256 MiningPoolReward
    {
        get => State.GetUInt256(nameof(MiningPoolReward));
        private set => State.SetUInt256(nameof(MiningPoolReward), value);
    }

    /// <inheritdoc />
    public bool Locked
    {
        get => State.GetBool(nameof(Locked));
        private set => State.SetBool(nameof(Locked), value);
    }

    /// <inheritdoc />
    public Address GetMiningPool(Address stakingPool)
    {
        return State.GetAddress($"MiningPool:{stakingPool}");
    }

    private void SetMiningPool(Address stakingPool, Address miningPool)
    {
        State.SetAddress($"MiningPool:{stakingPool}", miningPool);
    }
    
    /// <inheritdoc />
    public void NotifyDistribution(byte[] data)
    {
        EnsureUnlocked();
        EnsureSenderIsMinedToken();

        Notified = true;
        
        if (data.Length > 0)
        {
            var stakingPools = Serializer.ToArray<Address>(data);
        
            Assert(stakingPools.Length == MaximumNominations);
        
            var nominations = new Nomination[MaximumNominations];
        
            for (var i = 0; i < stakingPools.Length; i++)
            {
                nominations[i] = new Nomination {StakingPool = stakingPools[i]};
            
                Deploy(stakingPools[i]);
            }

            Nominations = nominations;
            
            SetMiningPoolRewardAmountExecute();
        }

        Unlock();
    }
    
    /// <inheritdoc />
    public void NominateLiquidityPool(Address stakingPool, UInt256 weight)
    {
        EnsureSenderIsMinedToken();
        
        if (Block.Number >= NominationPeriodEnd) return;
        
        var nomination = new Nomination {StakingPool = stakingPool, Weight = weight};
        var nominations = Nominations;
        var existingIndex = StakingPoolNominationIndex(nominations, stakingPool);
        var currentLength = nominations.Length;
            
        if (existingIndex >= 0)
        {
            nominations[existingIndex].Weight = weight;
        }
        else if (currentLength < MaximumNominations)
        {
            Array.Resize(ref nominations, currentLength + 1);           
            nominations[currentLength] = nomination;
        }
        else
        {
            var index = LowestNominationWeightIndex(nominations);
            var lowest = nominations[index];
            if (lowest.Weight >= nomination.Weight) return;
            nominations[index] = nomination;
        }

        Nominations = nominations;
        
        Log(new NominationLog
        {
            StakingPool = nomination.StakingPool, 
            Weight = nomination.Weight, 
            MiningPool = Deploy(nomination.StakingPool)
        });
    }

    /// <inheritdoc />
    public void RewardMiningPools()
    {
        EnsureUnlocked();
        EnsureNominationPeriodEnded();

        var nominations = Nominations;
        
        for (uint i = 0; i < MaximumNominations; i++)
        {
            if (nominations[i].StakingPool == Address.Zero) continue;
            
            RewardMiningPoolExecute(nominations[i], MiningPoolReward);
            IncrementMiningPoolsFunded();
        }

        ResetNominations();
        
        Unlock();
    }
    
    /// <inheritdoc />
    public void RewardMiningPool()
    {
        EnsureUnlocked();
        EnsureNominationPeriodEnded();
        
        var nominations = Nominations;
        
        for (uint i = 0; i < MaximumNominations; i++)
        {
            if (nominations[i].StakingPool == Address.Zero) continue;
            
            RewardMiningPoolExecute(nominations[i], MiningPoolReward);
            IncrementMiningPoolsFunded();
            
            if (i == MaximumNominations - 1)
            {
                ResetNominations();
            }
            else
            {
                nominations[i] = new Nomination();
                Nominations = nominations;
            }
            
            break;
        }
        
        Unlock();
    }
    
    private void RewardMiningPoolExecute(Nomination nomination, UInt256 reward)
    {
        var miningPool = GetMiningPool(nomination.StakingPool);
        
        SafeTransferTo(MinedToken, miningPool, reward);

        Assert(Call(miningPool, 0, nameof(IOpdexMiningPool.NotifyRewardAmount), new object[] { reward }).Success);
        
        Log(new RewardMiningPoolLog
        {
            StakingPool = nomination.StakingPool,
            MiningPool = miningPool,
            Amount = reward
        });
    }

    private void IncrementMiningPoolsFunded()
    {
        var miningPoolsFunded = MiningPoolsFunded;
        
        if (++miningPoolsFunded == MiningPoolsPerYear) SetMiningPoolRewardAmountExecute();
        else MiningPoolsFunded = miningPoolsFunded;
    }

    private void SetMiningPoolRewardAmountExecute()
    {
        Assert(Notified, "OPDEX: TOKEN_DISTRIBUTION_REQUIRED");

        var balance = (UInt256)Call(MinedToken, 0ul, nameof(IStandardToken.GetBalance), new object[] {Address}).ReturnValue;
        
        Assert(balance > MiningPoolsPerYear, "OPDEX: INVALID_BALANCE");

        MiningPoolReward = balance / MiningPoolsPerYear;
        MiningPoolsFunded = 0;
        Notified = false;
    }
    
    private Address Deploy(Address stakingPool)
    {
        var miningPool = GetMiningPool(stakingPool);

        if (miningPool != Address.Zero) return miningPool;

        miningPool = Create<OpdexMiningPool>(0ul, new object[] { Address, MinedToken, stakingPool, PeriodDuration }).NewContractAddress;
        
        SetMiningPool(stakingPool, miningPool);
        
        Log(new MiningPoolCreatedLog { MiningPool = miningPool, StakingPool = stakingPool });

        return miningPool;
    }
    
    private void ResetNominations()
    {
        Nominations = new Nomination[0];
        NominationPeriodEnd = Block.Number + PeriodDuration;
    }
    
    private static uint LowestNominationWeightIndex(Nomination[] nominations)
    {
        uint lowestIndex = 0;
        var lowestWeight = nominations[lowestIndex].Weight;

        for (uint i = 1; i < nominations.Length; i++)
        {
            if (nominations[i].Weight > lowestWeight) continue;
            
            lowestWeight = nominations[i].Weight;
            lowestIndex = i;
        }

        return lowestIndex;
    }

    private static int StakingPoolNominationIndex(Nomination[] nominations, Address stakingPool)
    {
        var max = nominations.Length < MaximumNominations ? (uint)nominations.Length : MaximumNominations;
        
        for (var i = 0; i < max; i++)
        {
            if (nominations[i].StakingPool == stakingPool) return i;
        }

        return -1;
    }

    private void SafeTransferTo(Address token, Address to, UInt256 amount)
    {
        if (amount == 0) return;
        
        var result = Call(token, 0, nameof(IStandardToken.TransferTo), new object[] {to, amount});
        
        Assert(result.Success && (bool)result.ReturnValue, "OPDEX: INVALID_TRANSFER_TO");
    }

    private void EnsureNominationPeriodEnded()
    {
        Assert(Block.Number > NominationPeriodEnd, "OPDEX: NOMINATION_PERIOD_ACTIVE");
    }

    private void EnsureSenderIsMinedToken()
    {
        Assert(Message.Sender == MinedToken, "OPDEX: INVALID_SENDER"); 
    }

    private void EnsureUnlocked()
    {
        Assert(!Locked, "OPDEX: LOCKED");
        Locked = true;
    }

    private void Unlock() => Locked = false;
}