using FluentAssertions;
using Moq;
using OpdexTokenTests.Base;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace OpdexTokenTests
{
    public class LiquidityStakingTests : TestBase
    {
        [Fact]
        public void CreatesContract_Success()
        {
            var staking = CreateNewLiquidityStaking();

            staking.RewardsDistribution.Should().Be(Factory);
            staking.RewardsToken.Should().Be(OPDX);
            staking.StakingToken.Should().Be(Pair);
            staking.RewardsDuration.Should().Be(287438);
            staking.TotalSupply.Should().Be(UInt256.Zero);
            staking.PeriodFinish.Should().Be(0);
            staking.LastUpdateTime.Should().Be(0);
            staking.RewardPerTokenStored.Should().Be(UInt256.Zero);
        }

        [Theory]
        [InlineData(99, 100, 99)]
        [InlineData(101, 100, 100)]
        public void LastTimeRewardApplicable_Success(ulong currentBlock, ulong periodFinish, ulong expected)
        {
            PersistentState.SetUInt64("PeriodFinish", periodFinish);

            var staking = CreateNewLiquidityStaking(currentBlock);

            staking.LastTimeRewardApplicable().Should().Be(expected);
        }

        [Theory]
        [InlineData(100, 200, 105, 101, 25_000_000_000, 100_000_000, 1_600_000)]
        [InlineData(100, 200, 200, 100, 10_000_000_000, 100_000_000, 100_000_000)]
        public void RewardPerToken_Success(ulong periodStart, ulong periodFinish, ulong currentBlock,
            ulong lastUpdateTime,
            UInt256 totalSupply, UInt256 rewardRate, UInt256 expected)
        {
            // Todo: Add to tests for this
            PersistentState.SetUInt256("RewardPerTokenStored", 0);
            PersistentState.SetUInt256("TotalSupply", totalSupply);
            PersistentState.SetUInt256("RewardRate", rewardRate);
            PersistentState.SetUInt64("LastUpdateTime", lastUpdateTime);
            PersistentState.SetUInt64("PeriodFinish", periodFinish);

            var staking = CreateNewLiquidityStaking(periodStart);

            SetupBlock(currentBlock);

            var rewardPerToken = staking.RewardPerToken();

            rewardPerToken.Should().Be(expected);
        }

        [Theory]
        [InlineData(100, 200, 105, 101, 25_000_000_000, 100_000_000, 400_000_000)]
        [InlineData(100, 200, 200, 100, 10_000_000_000, 100_000_000, 10_000_000_000)]
        public void Earned_Success(ulong periodStart, ulong periodFinish, ulong currentBlock, ulong lastUpdateTime,
            UInt256 totalSupply, UInt256 rewardRate, UInt256 expected)
        {
            PersistentState.SetUInt256("RewardPerTokenStored", 0);
            PersistentState.SetUInt256("TotalSupply", totalSupply);
            PersistentState.SetUInt256("RewardRate", rewardRate);
            PersistentState.SetUInt64("LastUpdateTime", lastUpdateTime);
            PersistentState.SetUInt64("PeriodFinish", periodFinish);
            
            // Todo: change this
            PersistentState.SetUInt256($"Balance:{Miner}", totalSupply);

            // Todo: Add tests for this
            PersistentState.SetUInt256($"UserRewardPerTokenPaid:{Miner}", UInt256.Zero);
            PersistentState.SetUInt256($"Reward:{Miner}", UInt256.Zero);

            var staking = CreateNewLiquidityStaking(periodStart);

            SetupBlock(currentBlock);

            var earned = staking.Earned(Miner);

            earned.Should().Be(expected);
        }

        // Todo: Added tests for adding to existing staking
        [Theory]
        [InlineData(100, 200, 105, 101, 25_000_000_000, 100_000_000, 100_000_000, 1_600_000)]
        [InlineData(100, 200, 200, 100, 10_000_000_000, 100_000_000, 100_000_000, 100_000_000)]
        public void Stake_Success(ulong periodStart, ulong periodFinish, ulong currentBlock, ulong lastUpdateTime,
            UInt256 totalSupply, UInt256 rewardRate, UInt256 amount, UInt256 userRewardPerTokenPaid)
        {
            PersistentState.SetUInt256("RewardPerTokenStored", 0);
            PersistentState.SetUInt256("TotalSupply", totalSupply);
            PersistentState.SetUInt256("RewardRate", rewardRate);
            PersistentState.SetUInt64("LastUpdateTime", lastUpdateTime);
            PersistentState.SetUInt64("PeriodFinish", periodFinish);

            var transferParams = new object[] { Miner, MiningContract, amount };
            SetupCall(Pair, 0ul, "TransferFrom", transferParams, TransferResult.Transferred(true));
            
            var staking = CreateNewLiquidityStaking(periodStart);

            SetupBlock(currentBlock);
            SetupMessage(MiningContract, Miner);

            staking.Stake(amount);

            staking.TotalSupply.Should().Be(totalSupply + amount);
            staking.GetBalance(Miner).Should().Be(amount);
            staking.GetReward(Miner).Should().Be(UInt256.Zero);
            staking.GetUserRewardPerTokenPaid(Miner).Should().Be(userRewardPerTokenPaid);

            VerifyCall(Pair, 0ul, "TransferFrom", transferParams, Times.Once);
            VerifyLog(new LiquidityStaking.StakedEvent { User = Miner, Amount = amount }, Times.Once);
        }

        [Theory]
        [InlineData(100, 200, 105, 101, 25_000_000_000, 100_000_000, 100_000_000)]
        [InlineData(100, 200, 200, 100, 10_000_000_000, 100_000_000, 100_000_000)]
        public void WithdrawSuccess(ulong periodStart, ulong periodFinish, ulong currentBlock, ulong lastUpdateTime,
            UInt256 totalSupply, UInt256 rewardRate, UInt256 amount)
        {
            PersistentState.SetUInt256("RewardPerTokenStored", 0);
            PersistentState.SetUInt256("TotalSupply", totalSupply);
            PersistentState.SetUInt256("RewardRate", rewardRate);
            PersistentState.SetUInt64("LastUpdateTime", lastUpdateTime);
            PersistentState.SetUInt64("PeriodFinish", periodFinish);

            var transferParams = new object[] { Miner, MiningContract, amount };
            SetupCall(Pair, 0ul, "TransferFrom", transferParams, TransferResult.Transferred(true));
            
            var staking = CreateNewLiquidityStaking(periodStart);

            SetupBlock(currentBlock);
            SetupMessage(MiningContract, Miner);

            staking.Stake(amount);

            var transferToParams = new object[] {Miner, amount};
            SetupCall(Pair, 0ul, "TransferTo", transferToParams, TransferResult.Transferred(true));
            
            staking.Withdraw(amount);

            staking.GetBalance(Miner).Should().Be(UInt256.Zero);
            
            VerifyCall(Pair, 0ul, "TransferTo", transferToParams, Times.Once);
            VerifyLog(new LiquidityStaking.WithdrawnEvent { User = Miner, Amount = amount }, Times.Once);
        }

        [Theory]
        // [InlineData(100, 200, 105, 101, 25_000_000_000, 100_000_000, 100_000_000, 0)]
        [InlineData(100, 200, 200, 100, 10_000_000_000, 100_000_000, 100_000_000, 0)]
        public void GetReward_Success(ulong periodStart, ulong periodFinish, ulong currentBlock, ulong lastUpdateTime,
            UInt256 totalSupply, UInt256 rewardRate, UInt256 amount, UInt256 reward)
        {
            // PersistentState.SetUInt256("RewardPerTokenStored", 0);
            PersistentState.SetUInt256("TotalSupply", totalSupply);
            PersistentState.SetUInt256("RewardRate", rewardRate);
            PersistentState.SetUInt64("LastUpdateTime", lastUpdateTime);
            PersistentState.SetUInt64("PeriodFinish", periodFinish);

            var transferParams = new object[] { Miner, MiningContract, amount };
            SetupCall(Pair, 0ul, "TransferFrom", transferParams, TransferResult.Transferred(true));
            
            var staking = CreateNewLiquidityStaking(periodStart);

            SetupBlock(currentBlock);
            SetupMessage(MiningContract, Miner);

            staking.Stake(amount);

            // var transferToParams = new object[] {Miner, amount};
            // SetupCall(Pair, 0ul, "TransferTo", transferToParams, TransferResult.Transferred(true));
            
            SetupBlock(periodFinish);
            
            // staking.Withdraw(amount);

            var transferToRewardParams = new object[] {Miner, reward};
            SetupCall(OPDX, 0ul, "TransferTo", transferToRewardParams, TransferResult.Transferred(true));
            
            staking.GetReward();
            
            VerifyCall(OPDX, 0ul, "TransferTo", transferToRewardParams, Times.Once);
            VerifyLog(new LiquidityStaking.RewardPaidEvent { User = Miner, Reward = reward }, Times.Once);
        }

        [Fact]
        public void Exit_Success()
        {
            
        }
    }
}