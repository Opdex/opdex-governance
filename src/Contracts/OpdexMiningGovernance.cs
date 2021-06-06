using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

/// <summary>
/// Mining governance contract that holds and distributes Opdex tokens to be mined. Mining pools are
/// activated and funded based on a liquidity pool nomination by Opdex token staking weight.
/// </summary>
public class OpdexMiningGovernance : SmartContract, IOpdexMiningGovernance
{
    private const uint MaxNominations = 4;
    private const uint MaxMiningPoolsFunded = 48;

    /// <summary>
    /// Constructor initializing opdex token mining governance contract.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="minedToken">The address of the token being mined.</param>
    /// <param name="miningDuration">The nomination and mining period block duration.</param>
    public OpdexMiningGovernance(ISmartContractState state, Address minedToken, ulong miningDuration) : base(state)
    {
        MinedToken = minedToken;
        MiningDuration = miningDuration;
    }

    /// <inheritdoc />
    public Address MinedToken
    {
        get => State.GetAddress(GovernanceStateKeys.MinedToken);
        private set => State.SetAddress(GovernanceStateKeys.MinedToken, value);
    }

    /// <inheritdoc />
    public bool Notified
    {
        get => State.GetBool(GovernanceStateKeys.Notified);
        private set => State.SetBool(GovernanceStateKeys.Notified, value);
    }

    /// <inheritdoc />
    public Nomination[] Nominations
    {
        get => State.GetArray<Nomination>(GovernanceStateKeys.Nominations);
        private set => State.SetArray(GovernanceStateKeys.Nominations, value);
    }

    /// <inheritdoc />
    public ulong MiningDuration
    {
        get => State.GetUInt64(GovernanceStateKeys.MiningDuration);
        private set => State.SetUInt64(GovernanceStateKeys.MiningDuration, value);
    }

    /// <inheritdoc />
    public ulong NominationPeriodEnd
    {
        get => State.GetUInt64(GovernanceStateKeys.NominationPeriodEnd);
        private set => State.SetUInt64(GovernanceStateKeys.NominationPeriodEnd, value);
    }

    /// <inheritdoc />
    public uint MiningPoolsFunded
    {
        get => State.GetUInt32(GovernanceStateKeys.MiningPoolsFunded);
        private set => State.SetUInt32(GovernanceStateKeys.MiningPoolsFunded, value);
    }

    /// <inheritdoc />
    public UInt256 MiningPoolReward
    {
        get => State.GetUInt256(GovernanceStateKeys.MiningPoolReward);
        private set => State.SetUInt256(GovernanceStateKeys.MiningPoolReward, value);
    }

    /// <inheritdoc />
    public bool Locked
    {
        get => State.GetBool(GovernanceStateKeys.Locked);
        private set => State.SetBool(GovernanceStateKeys.Locked, value);
    }

    /// <inheritdoc />
    public Address GetMiningPool(Address stakingPool)
    {
        return State.GetAddress($"{GovernanceStateKeys.MiningPool}:{stakingPool}");
    }

    private void SetMiningPool(Address stakingPool, Address miningPool)
    {
        State.SetAddress($"{GovernanceStateKeys.MiningPool}:{stakingPool}", miningPool);
    }

    /// <inheritdoc />
    public void NotifyDistribution(Address firstNomination, Address secondNomination, Address thirdNomination, Address fourthNomination)
    {
        EnsureSenderIsMinedToken();

        Notified = true;

        // (First distribution only) - Sets initial nominations for mining
        if (firstNomination != Address.Zero)
        {
            var stakingPools = new [] { firstNomination, secondNomination, thirdNomination, fourthNomination };

            var nominations = new Nomination[MaxNominations];

            for (var i = 0; i < stakingPools.Length; i++)
            {
                var pool = stakingPools[i];

                Assert(pool != Address.Zero, "OPDEX: INVALID_STAKING_POOL");

                nominations[i] = new Nomination {StakingPool = pool, Weight = 1};

                FindMiningPool(pool);
            }

            Nominations = nominations;

            SetMiningPoolRewardAmountExecute();

            NominationPeriodEnd = MiningDuration + Block.Number;
        }
    }

    /// <inheritdoc />
    public void NominateLiquidityPool(Address stakingPool, UInt256 weight)
    {
        EnsureSenderIsMinedToken();

        if (Block.Number >= NominationPeriodEnd) return;

        var nomination = new Nomination {StakingPool = stakingPool, Weight = weight};
        var nominations = Nominations;
        var existingIndex = StakingPoolNominationIndex(nominations, stakingPool);

        if (existingIndex >= 0)
        {
            nominations[existingIndex].Weight = weight;
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
            MiningPool = FindMiningPool(nomination.StakingPool)
        });
    }

    /// <inheritdoc />
    public void RewardMiningPools()
    {
        EnsureUnlocked();
        EnsureNominationPeriodEnded();

        var nominations = Nominations;

        for (uint i = 0; i < nominations.Length; i++)
        {
            if (nominations[i].Weight == UInt256.Zero) continue;

            RewardMiningPoolExecute(nominations[i].StakingPool, MiningPoolReward);
            IncrementMiningPoolsFunded();

            nominations[i].Weight = UInt256.Zero;
        }

        Nominations = nominations;

        ResetNominations();

        Unlock();
    }

    /// <inheritdoc />
    public void RewardMiningPool()
    {
        EnsureUnlocked();
        EnsureNominationPeriodEnded();

        var nominations = Nominations;

        for (uint i = 0; i < nominations.Length; i++)
        {
            if (nominations[i].Weight == UInt256.Zero) continue;

            RewardMiningPoolExecute(nominations[i].StakingPool, MiningPoolReward);
            IncrementMiningPoolsFunded();

            if (i == nominations.Length - 1)
            {
                ResetNominations();
            }
            else
            {
                nominations[i].Weight = UInt256.Zero;
                Nominations = nominations;
            }

            break;
        }

        Unlock();
    }

    private void RewardMiningPoolExecute(Address stakingPool, UInt256 reward)
    {
        var miningPool = GetMiningPool(stakingPool);

        SafeTransferTo(MinedToken, miningPool, reward);

        // Don't verify success, funds have already been sent. Protects potential failures from locking nomination distribution
        Call(miningPool, 0, "NotifyRewardAmount", new object[] {reward});

        Log(new RewardMiningPoolLog { StakingPool = stakingPool, MiningPool = miningPool, Amount = reward });
    }

    private void IncrementMiningPoolsFunded()
    {
        var miningPoolsFunded = MiningPoolsFunded;

        if (++miningPoolsFunded == MaxMiningPoolsFunded)
        {
            // When the 48th pool is funded, reset the mining pool reward amount, will require distribution
            SetMiningPoolRewardAmountExecute();
        }
        else
        {
            MiningPoolsFunded = miningPoolsFunded;
        }
    }

    private void SetMiningPoolRewardAmountExecute()
    {
        Assert(Notified, "OPDEX: TOKEN_DISTRIBUTION_REQUIRED");

        var balance = (UInt256)Call(MinedToken, 0ul, nameof(IStandardToken.GetBalance), new object[] {Address}).ReturnValue;

        // Minimum 1 satoshi per block
        Assert(balance > MaxMiningPoolsFunded * MiningDuration, "OPDEX: INVALID_BALANCE");

        MiningPoolReward = balance / MaxMiningPoolsFunded;
        MiningPoolsFunded = 0;
        Notified = false;
    }

    private Address FindMiningPool(Address stakingPool)
    {
        var miningPool = GetMiningPool(stakingPool);

        if (miningPool != Address.Zero) return miningPool;

        var miningPoolResponse = Call(stakingPool, 0ul, "get_MiningPool");

        miningPool = (Address)miningPoolResponse.ReturnValue;

        Assert(miningPoolResponse.Success && miningPool != Address.Zero, "OPDEX: INVALID_MINING_POOL");

        SetMiningPool(stakingPool, miningPool);

        return miningPool;
    }

    private void ResetNominations()
    {
        var nominations = Nominations;

        // Reset existing nominations to 1 weight, prevent lockup if no new nominations during RewardMiningPool(s)
        for (uint i = 0; i < nominations.Length; i++)
        {
            nominations[i].Weight = 1;
        }

        Nominations = nominations;
        NominationPeriodEnd = Block.Number + MiningDuration;
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
        for (var i = 0; i < nominations.Length; i++)
        {
            if (nominations[i].StakingPool == stakingPool)
            {
                return i;
            }
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