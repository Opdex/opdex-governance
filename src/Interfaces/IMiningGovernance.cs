using Stratis.SmartContracts;

public interface IMiningGovernance
{
    Address MinedToken { get; }
    ulong Genesis { get; }
    ulong NominationPeriodEnd { get; }
    uint BucketIndex { get; }
    UInt256 BucketReward { get; }
    bool Locked { get; }
    Nomination[] Nominations { get; }
    
    Address GetMiningPool(Address stakingToken);
    void Nominate(Address stakingToken, UInt256 weight);
    void SetBucketReward();
    void NotifyRewardAmounts();
    void NotifyNextRewardAmount();
}