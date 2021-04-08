using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

/// <summary>
/// Mining pool for staking Opdex liquidity pool tokens in order to earn new mined tokens.
/// </summary>
public class OpdexMiningPool : SmartContract, IOpdexMiningPool
{
    private const ulong SatsPerToken = 100_000_000;
    
    /// <summary>
    /// Constructor initializing a mining pool contract.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="miningGovernance">The mining governance address.</param>
    /// <param name="minedToken">The address of the token being mined.</param>
    /// <param name="stakingToken">The address of the liquidity pool token used for mining.</param>
    /// <param name="miningDuration">The duration of the mining period.</param>
    public OpdexMiningPool(
        ISmartContractState state, 
        Address miningGovernance, 
        Address minedToken, 
        Address stakingToken, 
        ulong miningDuration) : base(state)
    {
        MiningGovernance = miningGovernance;
        MinedToken = minedToken;
        StakingToken = stakingToken;
        MiningDuration = miningDuration;
    }

    /// <inheritdoc />
    public Address MiningGovernance
    {
        get => State.GetAddress(nameof(MiningGovernance));
        private set => State.SetAddress(nameof(MiningGovernance), value);
    }

    /// <inheritdoc />
    public Address StakingToken
    {
        get => State.GetAddress(nameof(StakingToken));
        private set => State.SetAddress(nameof(StakingToken), value);
    }

    /// <inheritdoc />
    public Address MinedToken
    {
        get => State.GetAddress(nameof(MinedToken));
        private set => State.SetAddress(nameof(MinedToken), value);
    }

    /// <inheritdoc />
    public ulong MiningPeriodEndBlock
    {
        get => State.GetUInt64(nameof(MiningPeriodEndBlock));
        private set => State.SetUInt64(nameof(MiningPeriodEndBlock), value);
    }
    
    /// <inheritdoc />
    public UInt256 RewardRate
    {
        get => State.GetUInt256(nameof(RewardRate));
        private set => State.SetUInt256(nameof(RewardRate), value);
    }
    
    /// <inheritdoc />
    public ulong MiningDuration
    {
        get => State.GetUInt64(nameof(MiningDuration));
        private set => State.SetUInt64(nameof(MiningDuration), value);
    }
    
    /// <inheritdoc />
    public ulong LastUpdateBlock
    {
        get => State.GetUInt64(nameof(LastUpdateBlock));
        private set => State.SetUInt64(nameof(LastUpdateBlock), value);
    }
    
    /// <inheritdoc />
    public UInt256 RewardPerToken
    {
        get => State.GetUInt256(nameof(RewardPerToken));
        private set => State.SetUInt256(nameof(RewardPerToken), value);
    }
    
    /// <inheritdoc />
    public UInt256 TotalSupply
    {
        get => State.GetUInt256(nameof(TotalSupply));
        private set => State.SetUInt256(nameof(TotalSupply), value);
    }
    
    /// <inheritdoc />
    public bool Locked
    {
        get => State.GetBool(nameof(Locked));
        private set => State.SetBool(nameof(Locked), value);
    }

    /// <inheritdoc />
    public UInt256 GetRewardPerTokenPaid(Address address)
    {
        return State.GetUInt256($"RewardPerTokenPaid:{address}");
    }

    private void SetRewardPerTokenPaid(Address address, UInt256 reward)
    {
        State.SetUInt256($"RewardPerTokenPaid:{address}", reward);
    }
    
    /// <inheritdoc />
    public UInt256 GetReward(Address address)
    {
        return State.GetUInt256($"Reward:{address}");
    }

    private void SetReward(Address address, UInt256 reward)
    {
        State.SetUInt256($"Reward:{address}", reward);
    }

    /// <inheritdoc />
    public UInt256 GetBalance(Address address)
    {
        return State.GetUInt256($"Balance:{address}");
    }

    private void SetBalance(Address address, UInt256 reward)
    {
        State.SetUInt256($"Balance:{address}", reward);
    }

    /// <inheritdoc />
    public ulong LatestBlockApplicable()
    {
        return Block.Number > MiningPeriodEndBlock ? MiningPeriodEndBlock : Block.Number;
    }

    /// <inheritdoc />
    public UInt256 GetRewardForDuration()
    {
        return RewardRate * MiningDuration;
    }

    /// <inheritdoc />
    public UInt256 GetRewardPerToken()
    {
        var totalSupply = TotalSupply;
        
        if (totalSupply == 0) return RewardPerToken;

        var remainingRewards = (LatestBlockApplicable() - LastUpdateBlock) * RewardRate;
        
        return RewardPerToken + (remainingRewards * SatsPerToken / totalSupply);
    }

    /// <inheritdoc />
    public UInt256 Earned(Address address)
    {
        var balance = GetBalance(address);
        var rewardPerToken = GetRewardPerToken();
        var addressRewardPaid = GetRewardPerTokenPaid(address);
        var remainingReward = rewardPerToken - addressRewardPaid;
        var reward = GetReward(address);
        
        return reward + (balance * remainingReward / SatsPerToken);
    }

    /// <inheritdoc />
    public void Mine(UInt256 amount)
    {
        EnsureUnlocked();
        Assert(amount > 0, "OPDEX: CANNOT_MINE_ZERO");

        UpdateReward(Message.Sender);
        
        TotalSupply += amount;
        
        SetBalance(Message.Sender, GetBalance(Message.Sender) + amount);

        SafeTransferFrom(StakingToken, Message.Sender, Address, amount);

        Log(new EnterMiningPoolLog { Miner = Message.Sender, Amount = amount });
        
        Unlock();
    }

    /// <inheritdoc />
    public void Collect()
    {
        EnsureUnlocked();
        
        UpdateReward(Message.Sender);

        CollectExecute();
        
        Unlock();
    }

    /// <inheritdoc />
    public void Exit()
    {
        EnsureUnlocked();

        UpdateReward(Message.Sender);

        var amount = GetBalance(Message.Sender);

        TotalSupply -= amount;
        
        SetBalance(Message.Sender, GetBalance(Message.Sender) - amount);
        
        SafeTransferTo(StakingToken, Message.Sender, amount);
        
        Log(new ExitMiningPoolLog { Miner = Message.Sender, Amount = amount });
        
        CollectExecute();
        
        Unlock();
    }
    
    /// <inheritdoc />
    public void NotifyRewardAmount(UInt256 reward)
    {
        EnsureUnlocked();
        Assert(Message.Sender == MiningGovernance, "OPDEX: UNAUTHORIZED");
        
        UpdateReward(Address.Zero);

        var miningDuration = MiningDuration;
        
        if (Block.Number >= MiningPeriodEndBlock)
        {
            RewardRate = reward / miningDuration;
        }
        else
        {
            var remaining = MiningPeriodEndBlock - Block.Number;
            var leftover = remaining * RewardRate;
            
            RewardRate = (reward + leftover) / miningDuration;
        }

        var balanceResult = Call(MinedToken, 0, nameof(IStandardToken.GetBalance), new object[] {Address});
        var balance = (UInt256)balanceResult.ReturnValue;
        
        Assert(balanceResult.Success && balance > 0, "OPDEX: INVALID_BALANCE");
        Assert(RewardRate <= balance / miningDuration, "OPDEX: PROVIDED_REWARD_TOO_HIGH");

        LastUpdateBlock = Block.Number;
        MiningPeriodEndBlock = Block.Number + miningDuration;
        
        Log(new MiningPoolRewardedLog { Amount = reward });
        
        Unlock();
    }

    private void CollectExecute()
    {
        var reward = GetReward(Message.Sender);

        if (reward > 0)
        {
            SetReward(Message.Sender, 0);
            
            SafeTransferTo(MinedToken, Message.Sender, reward);
            
            Log(new CollectMiningRewardsLog { Miner = Message.Sender, Amount = reward });
        }
    }

    private void UpdateReward(Address address)
    {
        var rewardPerToken = GetRewardPerToken();
        
        RewardPerToken = rewardPerToken;
        LastUpdateBlock = LatestBlockApplicable();

        if (address == Address.Zero) return;
        
        var earned = Earned(address);
        SetReward(address, earned);
            
        SetRewardPerTokenPaid(address, rewardPerToken);
    }
    
    private void SafeTransferTo(Address token, Address to, UInt256 amount)
    {
        if (amount == 0) return;
        
        var result = Call(token, 0, nameof(IStandardToken.TransferTo), new object[] {to, amount});
        
        Assert(result.Success && (bool)result.ReturnValue, "OPDEX: INVALID_TRANSFER_TO");
    }
    
    private void SafeTransferFrom(Address token, Address from, Address to, UInt256 amount)
    {
        if (amount == 0) return;
        
        var result = Call(token, 0, nameof(IStandardToken.TransferFrom), new object[] {from, to, amount});
        
        Assert(result.Success && (bool)result.ReturnValue, "OPDEX: INVALID_TRANSFER_FROM");
    }
    
    private void EnsureUnlocked()
    {
        Assert(!Locked, "OPDEX: LOCKED");
        Locked = true;
    }

    private void Unlock() => Locked = false;
}