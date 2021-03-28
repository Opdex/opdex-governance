using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

public class MiningPool : SmartContract, IMiningPool
{
    public MiningPool(ISmartContractState contractState, Address rewardsDistribution, Address rewardsToken, Address stakingToken) 
        : base(contractState)
    {
        RewardsDistribution = rewardsDistribution;
        MinedToken = rewardsToken;
        StakingToken = stakingToken;
        RewardsDuration = 328_500 - 41_062; // 8 weeks - 1 week
    }

    /// <inheritdoc />
    public Address RewardsDistribution
    {
        get => State.GetAddress(nameof(RewardsDistribution));
        private set => State.SetAddress(nameof(RewardsDistribution), value);
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
    public ulong PeriodFinish
    {
        get => State.GetUInt64(nameof(PeriodFinish));
        private set => State.SetUInt64(nameof(PeriodFinish), value);
    }
    
    /// <inheritdoc />
    public UInt256 RewardRate
    {
        get => State.GetUInt256(nameof(RewardRate));
        private set => State.SetUInt256(nameof(RewardRate), value);
    }
    
    /// <inheritdoc />
    public ulong RewardsDuration
    {
        get => State.GetUInt64(nameof(RewardsDuration));
        private set => State.SetUInt64(nameof(RewardsDuration), value);
    }
    
    /// <inheritdoc />
    public ulong LastUpdateBlock
    {
        get => State.GetUInt64(nameof(LastUpdateBlock));
        private set => State.SetUInt64(nameof(LastUpdateBlock), value);
    }
    
    /// <inheritdoc />
    public UInt256 RewardPerTokenStored
    {
        get => State.GetUInt256(nameof(RewardPerTokenStored));
        private set => State.SetUInt256(nameof(RewardPerTokenStored), value);
    }
    
    /// <inheritdoc />
    public UInt256 TotalSupply
    {
        get => State.GetUInt256(nameof(TotalSupply));
        private set => State.SetUInt256(nameof(TotalSupply), value);
    }

    /// <inheritdoc />
    public UInt256 GetUserRewardPerTokenPaid(Address user) 
        => State.GetUInt256($"UserRewardPerTokenPaid:{user}");

    private void SetUserRewardPerTokenPaid(Address user, UInt256 reward)
        => State.SetUInt256($"UserRewardPerTokenPaid:{user}", reward);
    
    /// <inheritdoc />
    public UInt256 GetReward(Address user)
        => State.GetUInt256($"Reward:{user}");

    private void SetReward(Address user, UInt256 reward)
        => State.SetUInt256($"Rewards:{user}", reward);
    
    /// <inheritdoc />
    public UInt256 GetBalance(Address user)
        => State.GetUInt256($"Balance:{user}");

    private void SetBalance(Address user, UInt256 reward)
        => State.SetUInt256($"Balance:{user}", reward);

    /// <inheritdoc />
    public ulong LastTimeRewardApplicable()
        => Block.Number > PeriodFinish ? PeriodFinish : Block.Number;
    
    /// <inheritdoc />
    public UInt256 GetRewardForDuration() => RewardRate * RewardsDuration;

    /// <inheritdoc />
    public UInt256 RewardPerToken()
    {
        if (TotalSupply == 0) return RewardPerTokenStored;

        var blocksToUpdate = LastTimeRewardApplicable() - LastUpdateBlock;
        
        var result = RewardPerTokenStored + ((blocksToUpdate * RewardRate * 100_000_000) / TotalSupply);

        return result;
    }

    /// <inheritdoc />
    public UInt256 Earned(Address address)
    {
        var balance = GetBalance(address);
        var rewardPerToken = RewardPerToken();
        var userRewardPaid = GetUserRewardPerTokenPaid(address);
        var remainingReward = rewardPerToken - userRewardPaid;
        var reward = GetReward(address);
        
        return balance * remainingReward / 100_000_000 + reward;
    }

    /// <inheritdoc />
    public void Mine(UInt256 amount)
    {
        UpdateReward(Message.Sender);
        
        Assert(amount > 0, "OPDEX: CANNOT_STAKE_ZERO");
        
        TotalSupply += amount;
        
        SetBalance(Message.Sender, GetBalance(Message.Sender) + amount);
        
        SafeTransferFrom(StakingToken, Message.Sender, Address, amount);

        Log(new StakedEvent { User = Message.Sender, Amount = amount });
    }

    /// <inheritdoc />
    public void Withdraw(UInt256 amount)
    {
        UpdateReward(Message.Sender);
        
        Assert(amount > 0, "OPDEX: CANNOT_WITHDRAW_ZERO");
        
        TotalSupply -= amount;
        
        SetBalance(Message.Sender, GetBalance(Message.Sender) - amount);
        
        SafeTransferTo(StakingToken, Message.Sender, amount);
        
        Log(new WithdrawnEvent { User = Message.Sender, Amount = amount });
    }

    /// <inheritdoc />
    public void GetReward()
    {
        UpdateReward(Message.Sender);

        var reward = GetReward(Message.Sender);

        if (reward > 0)
        {
            SetReward(Message.Sender, 0);
            
            SafeTransferTo(MinedToken, Message.Sender, reward);
            
            Log(new RewardPaidEvent { User = Message.Sender, Reward = reward });
        }
    }

    /// <inheritdoc />
    public void ExitMining()
    {
        Withdraw(GetBalance(Message.Sender));
        
        GetReward();
    }
    
    /// <inheritdoc />
    public void NotifyRewardAmount(UInt256 reward)
    {
        Assert(Message.Sender == RewardsDistribution);
        
        UpdateReward(Address.Zero);
        
        if (Block.Number >= PeriodFinish)
        {
            RewardRate = reward / RewardsDuration;
        }
        else
        {
            var remaining = PeriodFinish - Block.Number;
            var leftover = remaining * RewardRate;
            
            RewardRate = reward + leftover / RewardsDuration;
        }

        var balanceResult = Call(MinedToken, 0, nameof(IStandardToken.GetBalance), new object[] {Address});
        var balance = (UInt256) balanceResult.ReturnValue;
        
        Assert(balanceResult.Success && balance > 0);
        Assert(RewardRate <= balance / RewardsDuration, "OPDEX: PROVIDED_REWARD_TOO_HIGH");

        LastUpdateBlock = Block.Number;
        PeriodFinish = Block.Number + RewardsDuration;
        
        Log(new RewardAddedEvent { Reward = reward });
    }

    private void UpdateReward(Address account)
    {
        var rewardPerToken = RewardPerToken();
        
        RewardPerTokenStored = rewardPerToken;
        LastUpdateBlock = LastTimeRewardApplicable();

        if (account != Address.Zero)
        {
            var earned = Earned(account);
            SetReward(account, earned);
            
            SetUserRewardPerTokenPaid(account, rewardPerToken);
        }
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
}