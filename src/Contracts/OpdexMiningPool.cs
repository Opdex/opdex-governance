using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

/// <summary>
/// 
/// </summary>
public class OpdexMiningPool : SmartContract, IOpdexMiningPool
{
    /// <summary>
    /// Constructor initializing a mining pool contract.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="miningGovernance"></param>
    /// <param name="minedToken"></param>
    /// <param name="stakingToken"></param>
    public OpdexMiningPool(ISmartContractState state, Address miningGovernance, Address minedToken, Address stakingToken) : base(state)
    {
        MiningGovernance = miningGovernance;
        MinedToken = minedToken;
        StakingToken = stakingToken;
        MiningDuration = 328_500 - 41_062; // 8 weeks - 1 week
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
        State.SetUInt256($"Rewards:{address}", reward);
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
        if (TotalSupply == 0) return RewardPerToken;

        var blocksToUpdate = LatestBlockApplicable() - LastUpdateBlock;
        
        var result = RewardPerToken + ((blocksToUpdate * RewardRate * 100_000_000) / TotalSupply);

        return result;
    }

    /// <inheritdoc />
    public UInt256 Earned(Address address)
    {
        var balance = GetBalance(address);
        var rewardPerToken = GetRewardPerToken();
        var addressRewardPaid = GetRewardPerTokenPaid(address);
        var remainingReward = rewardPerToken - addressRewardPaid;
        var reward = GetReward(address);
        
        return balance * remainingReward / 100_000_000 + reward;
    }

    /// <inheritdoc />
    public void Mine(UInt256 amount)
    {
        EnsureUnlocked();
        
        UpdateReward(Message.Sender);
        
        Assert(amount > 0, "OPDEX: CANNOT_STAKE_ZERO");
        
        TotalSupply += amount;
        
        SetBalance(Message.Sender, GetBalance(Message.Sender) + amount);

        SafeTransferFrom(StakingToken, Message.Sender, Address, amount);

        Log(new StakedEvent { To = Message.Sender, Amount = amount });
        
        Unlock();
    }

    /// <inheritdoc />
    public void Withdraw(UInt256 amount)
    {
        EnsureUnlocked();
        
        UpdateReward(Message.Sender);
        
        Assert(amount > 0, "OPDEX: CANNOT_WITHDRAW_ZERO");
        
        TotalSupply -= amount;
        
        SetBalance(Message.Sender, GetBalance(Message.Sender) - amount);
        
        SafeTransferTo(StakingToken, Message.Sender, amount);
        
        Log(new WithdrawnEvent { To = Message.Sender, Amount = amount });
        
        Unlock();
    }

    /// <inheritdoc />
    public void Collect()
    {
        EnsureUnlocked();
        
        UpdateReward(Message.Sender);

        var reward = GetReward(Message.Sender);

        if (reward > 0)
        {
            SetReward(Message.Sender, 0);
            
            SafeTransferTo(MinedToken, Message.Sender, reward);
            
            Log(new RewardPaidEvent { To = Message.Sender, Amount = reward });
        }
        
        Unlock();
    }

    /// <inheritdoc />
    public void ExitMining()
    {
        Withdraw(GetBalance(Message.Sender));
        Collect();
    }
    
    /// <inheritdoc />
    public void NotifyRewardAmount(UInt256 reward)
    {
        EnsureUnlocked();
        Assert(Message.Sender == MiningGovernance);
        
        UpdateReward(Address.Zero);
        
        if (Block.Number >= MiningPeriodEndBlock)
        {
            RewardRate = reward / MiningDuration;
        }
        else
        {
            var remaining = MiningPeriodEndBlock - Block.Number;
            var leftover = remaining * RewardRate;
            
            RewardRate = reward + leftover / MiningDuration;
        }

        var balanceResult = Call(MinedToken, 0, nameof(IStandardToken.GetBalance), new object[] {Address});
        var balance = (UInt256)balanceResult.ReturnValue;
        
        Assert(balanceResult.Success && balance > 0);
        Assert(RewardRate <= balance / MiningDuration, "OPDEX: PROVIDED_REWARD_TOO_HIGH");

        LastUpdateBlock = Block.Number;
        MiningPeriodEndBlock = Block.Number + MiningDuration;
        
        Log(new RewardAddedEvent { Reward = reward });
        
        Unlock();
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