using Stratis.SmartContracts;

public interface IMiningPool
{
    /// <summary>
    /// 
    /// </summary>
    Address RewardsDistribution { get; }
    
    /// <summary>
    /// 
    /// </summary>
    Address StakingToken { get; }
    
    /// <summary>
    /// 
    /// </summary>
    Address MinedToken { get; }
    
    /// <summary>
    /// 
    /// </summary>
    ulong PeriodFinish { get; }
    
    /// <summary>
    /// 
    /// </summary>
    UInt256 RewardRate { get; }
    
    /// <summary>
    /// 
    /// </summary>
    ulong RewardsDuration { get; }
    
    /// <summary>
    /// 
    /// </summary>
    ulong LastUpdateBlock { get; }
    
    /// <summary>
    /// 
    /// </summary>
    UInt256 RewardPerTokenStored { get; }
    
    /// <summary>
    /// 
    /// </summary>
    UInt256 TotalSupply { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    UInt256 GetUserRewardPerTokenPaid(Address user);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    UInt256 GetReward(Address user);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    UInt256 GetBalance(Address user);

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    ulong LastTimeRewardApplicable();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    UInt256 GetRewardForDuration();

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    UInt256 RewardPerToken();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    UInt256 Earned(Address address);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="amount"></param>
    void Mine(UInt256 amount);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="amount"></param>
    void Withdraw(UInt256 amount);
    
    /// <summary>
    /// 
    /// </summary>
    void GetReward();
    
    /// <summary>
    /// 
    /// </summary>
    void ExitMining();
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="reward"></param>
    void NotifyRewardAmount(UInt256 reward);
}