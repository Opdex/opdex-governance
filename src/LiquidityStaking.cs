using Stratis.SmartContracts;

public class LiquidityStaking : SmartContract
{
    public LiquidityStaking(ISmartContractState contractState, Address rewardsDistribution, Address rewardsToken, Address stakingToken) 
        : base(contractState)
    {
        RewardsDistribution = rewardsDistribution;
        RewardsToken = rewardsToken;
        StakingToken = stakingToken;
    }

    public Address RewardsDistribution
    {
        get => State.GetAddress(nameof(RewardsDistribution));
        private set => State.SetAddress(nameof(RewardsDistribution), value);
    }

    public Address StakingToken
    {
        get => State.GetAddress(nameof(StakingToken));
        private set => State.SetAddress(nameof(StakingToken), value);
    }

    public Address RewardsToken
    {
        get => State.GetAddress(nameof(RewardsToken));
        private set => State.SetAddress(nameof(RewardsToken), value);
    }

    public ulong PeriodFinish
    {
        get => State.GetUInt64(nameof(PeriodFinish));
        private set => State.SetUInt64(nameof(PeriodFinish), value);
    }
    
    public UInt256 RewardRate
    {
        get => State.GetUInt256(nameof(RewardRate));
        private set => State.SetUInt256(nameof(RewardRate), value);
    }

    public ulong RewardsDuration
    {
        get => State.GetUInt64(nameof(RewardsDuration));
        private set => State.SetUInt64(nameof(RewardsDuration), value);
    }
    
    public ulong LastUpdateTime
    {
        get => State.GetUInt64(nameof(LastUpdateTime));
        private set => State.SetUInt64(nameof(LastUpdateTime), value);
    }
    
    public UInt256 RewardPerTokenStored
    {
        get => State.GetUInt256(nameof(RewardPerTokenStored));
        private set => State.SetUInt256(nameof(RewardPerTokenStored), value);
    }
    
    public UInt256 TotalSupply
    {
        get => State.GetUInt256(nameof(TotalSupply));
        private set => State.SetUInt256(nameof(TotalSupply), value);
    }

    public UInt256 GetUserRewardPerTokenPaid(Address user)
    {
        return State.GetUInt256($"UserRewardPerTokenPaid:{user}");
    }

    private void SetUserRewardPerTokenPaid(Address user, UInt256 reward)
    {
        State.SetUInt256($"UserRewardPerTokenPaid:{user}", reward);
    }
    
    public UInt256 GetReward(Address user)
    {
        return State.GetUInt256($"Reward:{user}");
    }

    private void SetReward(Address user, UInt256 reward)
    {
        State.SetUInt256($"Rewards:{user}", reward);
    }
    
    public UInt256 GetBalance(Address user)
    {
        return State.GetUInt256($"Balance:{user}");
    }

    private void SetBalance(Address user, UInt256 reward)
    {
        State.SetUInt256($"Balance:{user}", reward);
    }

    public ulong LastTimeRewardApplicable()
    {
        return Block.Number > PeriodFinish ? PeriodFinish : Block.Number;
    }

    public UInt256 RewardPerToken()
    {
        if (TotalSupply == 0)
        {
            return RewardPerTokenStored;
        }
        
        return RewardPerTokenStored + ((LastTimeRewardApplicable() - LastUpdateTime * RewardRate * 100_000_000) / TotalSupply);
    }

    public UInt256 Earned(Address address)
    {
        return GetBalance(address) * (RewardPerToken() - GetUserRewardPerTokenPaid(address)) / 100_000_000 + GetReward(address);
    }

    public UInt256 GetRewardForDuration()
    {
        return RewardRate * RewardsDuration;
    }
    
    public void Stake(UInt256 amount)
    {
        var sender = Message.Sender;
        UpdateReward(sender);
        
        Assert(amount > 0, "OPDEX: CANNOT_STAKE_ZERO");
        TotalSupply += amount;
        SetBalance(sender, GetBalance(sender) + amount);
        
        SafeTransferFrom(StakingToken, sender, Address, amount);

        Log(new StakedEvent
        {
            User = sender,
            Amount = amount
        });
    }

    public void Withdraw(UInt256 amount)
    {
        var sender = Message.Sender;
        UpdateReward(sender);
        
        Assert(amount > 0, "OPDEX: CANNOT_WITHDRAW_ZERO");
        TotalSupply -= amount;
        SetBalance(sender, GetBalance(sender) - amount);
        
        SafeTransferTo(StakingToken, sender, amount);
        
        Log( new WithdrawnEvent
        {
            User = sender,
            Amount = amount
        });
    }

    public void GetReward()
    {
        var sender = Message.Sender;
        UpdateReward(sender);

        var reward = GetReward(sender);

        if (reward > 0)
        {
            SetReward(sender, 0);
            SafeTransferTo(RewardsToken, sender, reward);
            Log(new RewardPaidEvent
            {
                User = sender,
                Reward = reward
            });
        }
    }

    public void Exit()
    {
        Withdraw(GetBalance(Message.Sender));
        GetReward();
    }

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

        var balanceResult = Call(RewardsToken, 0, "GetBalance", new object[] {Address});
        var balance = (UInt256) balanceResult.ReturnValue;
        Assert(balanceResult.Success && balance > 0);
        Assert(RewardRate <= balance / RewardsDuration, "OPDEX: PROVIDED_REWARD_TOO_HIGH");

        LastUpdateTime = Block.Number;
        PeriodFinish = Block.Number + RewardsDuration;
        
        Log(new RewardAddedEvent { Reward = reward });
    }

    private void UpdateReward(Address account)
    {
        RewardPerTokenStored = RewardPerToken();
        LastUpdateTime = LastTimeRewardApplicable();

        if (account != Address.Zero)
        {
            SetReward(account, Earned(account));
            SetUserRewardPerTokenPaid(account, RewardPerTokenStored);
        }
    }
    
    private void SafeTransferTo(Address token, Address to, UInt256 amount)
    {
        if (amount == 0) return;
        
        var result = Call(token, 0, "TransferTo", new object[] {to, amount});
        
        Assert(result.Success && (bool)result.ReturnValue, "OPDEX: INVALID_TRANSFER_TO");
    }
    
    private void SafeTransferFrom(Address token, Address from, Address to, UInt256 amount)
    {
        if (amount == 0) return;
        
        var result = Call(token, 0, "TransferFrom", new object[] {from, to, amount});
        
        Assert(result.Success && (bool)result.ReturnValue, "OPDEX: INVALID_TRANSFER_FROM");
    }

    public struct RewardAddedEvent
    {
        [Index] public UInt256 Reward;
    }

    public struct StakedEvent
    {
        [Index] public Address User;
        public UInt256 Amount;
    }

    public struct WithdrawnEvent
    {
        [Index] public Address User;
        public UInt256 Amount;
    }

    public struct RewardPaidEvent
    {
        [Index] public Address User;
        public UInt256 Reward;
    }
}