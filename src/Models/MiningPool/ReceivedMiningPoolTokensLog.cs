using Stratis.SmartContracts;

public struct ReceivedMiningPoolTokensLog
{
    public UInt256 Amount;
    public UInt256 RewardRate;
    public ulong MiningPeriodEndBlock;
}