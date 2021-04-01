using FluentAssertions;
using Moq;
using OpdexTokenTests.Base;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace OpdexTokenTests
{
    public class OpdexMiningPoolTests : TestBase
    {
        [Fact]
        public void CreatesContract_Success()
        {
            var staking = CreateNewMiningPool();

            staking.MiningGovernance.Should().Be(MiningGovernance);
            staking.MinedToken.Should().Be(OPDX);
            staking.StakingToken.Should().Be(Pool1);
            staking.MiningDuration.Should().Be(287438);
            staking.TotalSupply.Should().Be(UInt256.Zero);
            staking.MiningPeriodEndBlock.Should().Be(0);
            staking.LastUpdateBlock.Should().Be(0);
            staking.RewardPerToken.Should().Be(UInt256.Zero);
        }

        [Theory]
        [InlineData(99, 100, 99)]
        [InlineData(101, 100, 100)]
        public void LastTimeRewardApplicable_Success(ulong currentBlock, ulong periodFinish, ulong expected)
        {
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodFinish);

            var staking = CreateNewMiningPool(currentBlock);

            staking.LatestBlockApplicable().Should().Be(expected);
        }

        [Theory]
        [InlineData(100, 200, 105, 101, 25_000_000_000, 100_000_000, 1_600_000)]
        [InlineData(100, 200, 200, 100, 10_000_000_000, 100_000_000, 100_000_000)]
        public void RewardPerToken_Success(ulong periodStart, ulong periodFinish, ulong currentBlock,
            ulong lastUpdateTime,
            UInt256 totalSupply, UInt256 rewardRate, UInt256 expected)
        {
            // Todo: Add to tests for this
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.RewardPerToken), 0);
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.TotalSupply), totalSupply);
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.LastUpdateBlock), lastUpdateTime);
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodFinish);

            var staking = CreateNewMiningPool(periodStart);

            SetupBlock(currentBlock);

            var rewardPerToken = staking.GetRewardPerToken();

            rewardPerToken.Should().Be(expected);
        }

        [Theory]
        [InlineData(100, 200, 105, 101, 25_000_000_000, 100_000_000, 400_000_000)]
        [InlineData(100, 200, 200, 100, 10_000_000_000, 100_000_000, 10_000_000_000)]
        public void Earned_Success(ulong periodStart, ulong periodFinish, ulong currentBlock, ulong lastUpdateTime,
            UInt256 totalSupply, UInt256 rewardRate, UInt256 expected)
        {
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.RewardPerToken), 0);
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.TotalSupply), totalSupply);
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.LastUpdateBlock), lastUpdateTime);
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodFinish);
            
            // Todo: change this
            PersistentState.SetUInt256($"Balance:{Miner1}", totalSupply);

            // Todo: Add tests for this
            PersistentState.SetUInt256($"UserRewardPerTokenPaid:{Miner1}", UInt256.Zero);
            PersistentState.SetUInt256($"Reward:{Miner1}", UInt256.Zero);

            var staking = CreateNewMiningPool(periodStart);

            SetupBlock(currentBlock);

            var earned = staking.Earned(Miner1);

            earned.Should().Be(expected);
        }

        // Todo: Added tests for adding to existing staking
        [Theory]
        [InlineData(100, 200, 105, 101, 25_000_000_000, 100_000_000, 100_000_000, 1_600_000)]
        [InlineData(100, 200, 200, 100, 10_000_000_000, 100_000_000, 100_000_000, 100_000_000)]
        public void Mine_Success(ulong periodStart, ulong periodFinish, ulong currentBlock, ulong lastUpdateTime,
            UInt256 totalSupply, UInt256 rewardRate, UInt256 amount, UInt256 userRewardPerTokenPaid)
        {
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.RewardPerToken), 0);
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.TotalSupply), totalSupply);
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.LastUpdateBlock), lastUpdateTime);
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodFinish);

            var transferParams = new object[] { Miner1, MiningPool1, amount };
            SetupCall(Pool1, 0ul, "TransferFrom", transferParams, TransferResult.Transferred(true));
            
            var staking = CreateNewMiningPool(periodStart);

            SetupBlock(currentBlock);
            SetupMessage(MiningPool1, Miner1);

            staking.Mine(amount);

            staking.TotalSupply.Should().Be(totalSupply + amount);
            staking.GetBalance(Miner1).Should().Be(amount);
            staking.GetReward(Miner1).Should().Be(UInt256.Zero);
            staking.GetRewardPerTokenPaid(Miner1).Should().Be(userRewardPerTokenPaid);

            VerifyCall(Pool1, 0ul, "TransferFrom", transferParams, Times.Once);
            VerifyLog(new StakedEvent { To = Miner1, Amount = amount }, Times.Once);
        }

        [Theory]
        [InlineData(100, 200, 105, 101, 25_000_000_000, 100_000_000, 100_000_000)]
        [InlineData(100, 200, 200, 100, 10_000_000_000, 100_000_000, 100_000_000)]
        public void WithdrawSuccess(ulong periodStart, ulong periodFinish, ulong currentBlock, ulong lastUpdateTime,
            UInt256 totalSupply, UInt256 rewardRate, UInt256 amount)
        {
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.RewardPerToken), 0);
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.TotalSupply), totalSupply);
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.LastUpdateBlock), lastUpdateTime);
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodFinish);

            var transferParams = new object[] { Miner1, MiningPool1, amount };
            SetupCall(Pool1, 0ul, "TransferFrom", transferParams, TransferResult.Transferred(true));
            
            var staking = CreateNewMiningPool(periodStart);

            SetupBlock(currentBlock);
            SetupMessage(MiningPool1, Miner1);

            staking.Mine(amount);

            var transferToParams = new object[] {Miner1, amount};
            SetupCall(Pool1, 0ul, "TransferTo", transferToParams, TransferResult.Transferred(true));
            
            staking.Withdraw(amount);

            staking.GetBalance(Miner1).Should().Be(UInt256.Zero);
            
            VerifyCall(Pool1, 0ul, "TransferTo", transferToParams, Times.Once);
            VerifyLog(new WithdrawnEvent { To = Miner1, Amount = amount }, Times.Once);
        }

        [Theory]
        // [InlineData(100, 200, 105, 101, 25_000_000_000, 100_000_000, 100_000_000, 0)]
        [InlineData(100, 200, 200, 100, 10_000_000_000, 100_000_000, 100_000_000, 0)]
        public void GetReward_Success(ulong periodStart, ulong periodFinish, ulong currentBlock, ulong lastUpdateTime,
            UInt256 totalSupply, UInt256 rewardRate, UInt256 amount, UInt256 reward)
        {
            // PersistentState.SetUInt256(nameof(IOpdexMiningPool.RewardPerToken), 0);
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.TotalSupply), totalSupply);
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.LastUpdateBlock), lastUpdateTime);
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodFinish);

            var transferParams = new object[] { Miner1, MiningPool1, amount };
            SetupCall(Pool1, 0ul, "TransferFrom", transferParams, TransferResult.Transferred(true));
            
            var staking = CreateNewMiningPool(periodStart);

            SetupBlock(currentBlock);
            SetupMessage(MiningPool1, Miner1);

            staking.Mine(amount);

            // var transferToParams = new object[] {Miner, amount};
            // SetupCall(Pool1, 0ul, "TransferTo", transferToParams, TransferResult.Transferred(true));
            
            SetupBlock(periodFinish);
            
            // staking.Withdraw(amount);

            var transferToRewardParams = new object[] {Miner1, reward};
            SetupCall(OPDX, 0ul, "TransferTo", transferToRewardParams, TransferResult.Transferred(true));
            
            staking.Collect();
            
            VerifyCall(OPDX, 0ul, "TransferTo", transferToRewardParams, Times.Once);
            VerifyLog(new RewardPaidEvent { To = Miner1, Amount = reward }, Times.Once);
        }

        [Fact]
        public void Exit_Success()
        {
            
        }
    }
}