using FluentAssertions;
using Moq;
using OpdexGovernanceTests.Base;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Standards;
using Xunit;

namespace OpdexGovernanceTests
{
    public class OpdexMiningPoolTests : TestBase
    {
        [Fact]
        public void CreatesContract_Success()
        {
            var miningPool = CreateNewMiningPool();

            miningPool.MiningGovernance.Should().Be(MiningGovernance);
            miningPool.MinedToken.Should().Be(ODX);
            miningPool.StakingToken.Should().Be(Pool1);
            miningPool.MiningDuration.Should().Be(BlocksPerMonth);
            miningPool.TotalSupply.Should().Be(UInt256.Zero);
            miningPool.MiningPeriodEndBlock.Should().Be(0);
            miningPool.LastUpdateBlock.Should().Be(0);
            miningPool.RewardPerToken.Should().Be(UInt256.Zero);
        }

        [Fact]
        public void GetRewardForDuration_Success()
        {
            const ulong miningDuration = 100;
            UInt256 rewardRate = 10;
            UInt256 expected = 1000;

            var miningPool = CreateNewMiningPool(100);
            
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.MiningDuration), miningDuration);
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);

            miningPool.GetRewardForDuration().Should().Be(expected);
        }

        [Theory]
        [InlineData(99, 100, 99)]
        [InlineData(101, 100, 100)]
        public void LastTimeRewardApplicable_Success(ulong currentBlock, ulong periodFinish, ulong expected)
        {
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodFinish);

            var miningPool = CreateNewMiningPool(currentBlock);

            miningPool.LatestBlockApplicable().Should().Be(expected);
        }

        [Theory]
        [InlineData(100, 200, 105, 101, 25_000_000_000, 100_000_000, 1_600_000)]
        [InlineData(100, 200, 200, 100, 10_000_000_000, 100_000_000, 100_000_000)]
        [InlineData(100, 200, 200, 110, 10_000_000_000, 100_000_000, 90_000_000)]
        [InlineData(100, 200, 200, 110, 0, 100_000_000, 0)]
        public void RewardPerToken_Success(ulong periodStart, ulong periodFinish, ulong currentBlock,
            ulong lastUpdateBlock, UInt256 totalSupply, UInt256 rewardRate, UInt256 expected)
        {
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.RewardPerToken), 0);
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.TotalSupply), totalSupply);
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.LastUpdateBlock), lastUpdateBlock);
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodFinish);

            var miningPool = CreateNewMiningPool(periodStart);

            SetupBlock(currentBlock);

            var rewardPerToken = miningPool.GetRewardPerToken();

            rewardPerToken.Should().Be(expected);
        }

        [Theory]
        [InlineData(100, 200, 105, 101, 25_000_000_000, 100_000_000, 400_000_000)]
        [InlineData(100, 200, 200, 100, 10_000_000_000, 100_000_000, 10_000_000_000)]
        public void Earned_Success(ulong periodStart, ulong periodFinish, ulong currentBlock, ulong lastUpdateBlock,
            UInt256 totalSupply, UInt256 rewardRate, UInt256 expected)
        {
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.RewardPerToken), 0);
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.TotalSupply), totalSupply);
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.LastUpdateBlock), lastUpdateBlock);
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodFinish);
            
            // Todo: change this
            PersistentState.SetUInt256($"Balance:{Miner1}", totalSupply);

            // Todo: Add tests for this
            PersistentState.SetUInt256($"UserRewardPerTokenPaid:{Miner1}", UInt256.Zero);
            PersistentState.SetUInt256($"Reward:{Miner1}", UInt256.Zero);

            var miningPool = CreateNewMiningPool(periodStart);

            SetupBlock(currentBlock);

            var earned = miningPool.Earned(Miner1);

            earned.Should().Be(expected);
        }

        [Theory]
        [InlineData(100, 200, 105, 101, 25_000_000_000, 100_000_000, 100_000_000, 1_600_000)]
        [InlineData(100, 200, 200, 100, 10_000_000_000, 100_000_000, 100_000_000, 100_000_000)]
        public void Mine_NewMiner_Success(ulong periodStart, ulong periodFinish, ulong currentBlock, ulong lastUpdateBlock,
            UInt256 totalSupply, UInt256 rewardRate, UInt256 amount, UInt256 userRewardPerTokenPaid)
        {
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.RewardPerToken), 0);
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.TotalSupply), totalSupply);
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.LastUpdateBlock), lastUpdateBlock);
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodFinish);

            var transferParams = new object[] { Miner1, MiningPool1, amount };
            SetupCall(Pool1, 0ul, "TransferFrom", transferParams, TransferResult.Transferred(true));
            
            var miningPool = CreateNewMiningPool(periodStart);

            SetupBlock(currentBlock);
            SetupMessage(MiningPool1, Miner1);

            miningPool.Mine(amount);

            miningPool.TotalSupply.Should().Be(totalSupply + amount);
            miningPool.GetBalance(Miner1).Should().Be(amount);
            miningPool.GetReward(Miner1).Should().Be(UInt256.Zero);
            miningPool.GetRewardPerTokenPaid(Miner1).Should().Be(userRewardPerTokenPaid);

            VerifyCall(Pool1, 0ul, "TransferFrom", transferParams, Times.Once);
            VerifyLog(new StartMiningLog { Miner = Miner1, Amount = amount }, Times.Once);
        }

        [Theory]
        [InlineData(100, 200, 150, 101, 25_000_000_000, 100_000_000, 100_000_000, 19_600_000)]
        [InlineData(100, 200, 200, 100, 10_000_000_000, 100_000_000, 100_000_000, 100_000_000)]
        public void Mine_AddToExistingPosition_Success(ulong periodStart, ulong periodFinish, ulong currentBlock, ulong lastUpdateBlock,
            UInt256 totalSupply, UInt256 rewardRate, UInt256 amount, UInt256 userRewardPerTokenPaid)
        {
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.RewardPerToken), 0);
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.TotalSupply), totalSupply);
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.LastUpdateBlock), lastUpdateBlock);
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodFinish);
            PersistentState.SetUInt256($"Balance:{Miner1}", amount);

            var transferParams = new object[] { Miner1, MiningPool1, amount };
            SetupCall(Pool1, 0ul, "TransferFrom", transferParams, TransferResult.Transferred(true));
            
            var miningPool = CreateNewMiningPool(periodStart);

            SetupBlock(currentBlock);
            SetupMessage(MiningPool1, Miner1);

            miningPool.Mine(amount);

            miningPool.TotalSupply.Should().Be(totalSupply + amount);
            miningPool.GetBalance(Miner1).Should().Be(amount * 2); // previous amount + same amount added again
            miningPool.GetReward(Miner1).Should().Be(userRewardPerTokenPaid);
            miningPool.GetRewardPerTokenPaid(Miner1).Should().Be(userRewardPerTokenPaid);

            VerifyCall(Pool1, 0ul, "TransferFrom", transferParams, Times.Once);
            VerifyLog(new StartMiningLog { Miner = Miner1, Amount = amount }, Times.Once);
        }

        [Fact]
        public void Mine_Throws_CannotMineZero()
        {
            var miningPool = CreateNewMiningPool(100);

            SetupBlock(101);
            SetupMessage(MiningPool1, Miner1);

            miningPool.Invoking(s => s.Mine(0))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: CANNOT_MINE_ZERO");
        }
        
        [Fact]
        public void Mine_Throws_Locked()
        {
            var miningPool = CreateNewMiningPool(100);

            SetupBlock(101);
            SetupMessage(MiningPool1, Miner1);
            
            PersistentState.SetBool(nameof(IOpdexMiningPool.Locked), true);

            miningPool.Invoking(s => s.Mine(123))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: LOCKED");
        }
        
        [Theory]
        [InlineData(100, 200, 150, 101, 25_000_000_000, 100_000_000, 100_000_000, 19_600_000)]
        [InlineData(100, 200, 200, 100, 10_000_000_000, 100_000_000, 100_000_000, 100_000_000)]
        [InlineData(100, 200, 125, 125, 10_000_000_000, 100_000_000, 100_000_000, 0)]
        [InlineData(100, 200, 126, 125, 10_000_000_000, 100_000_000, 100_000_000, 1_000_000)]
        public void Collect_Success(ulong periodStart, ulong periodFinish, ulong currentBlock, ulong lastUpdateBlock,
            UInt256 totalSupply, UInt256 rewardRate, UInt256 amount, UInt256 expectedReward)
        {
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.RewardPerToken), UInt256.Zero);
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.TotalSupply), totalSupply);
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);
            PersistentState.SetUInt256($"Balance:{Miner1}", amount);
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.LastUpdateBlock), lastUpdateBlock);
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodFinish);

            var transferParams = new object[] { Miner1, expectedReward };
            SetupCall(ODX, 0ul, nameof(IStandardToken256.TransferTo), transferParams, TransferResult.Transferred(true));
            
            var miningPool = CreateNewMiningPool(periodStart);

            SetupBlock(currentBlock);
            SetupMessage(MiningPool1, Miner1);

            miningPool.Collect();

            miningPool.GetReward(Miner1).Should().Be(UInt256.Zero);
            
            if (expectedReward > 0)
            {
                VerifyCall(ODX, 0, nameof(IStandardToken256.TransferTo), transferParams, Times.Once);
                VerifyLog(new CollectMiningRewardsLog { Miner = Miner1, Amount = expectedReward }, Times.Once);
            }
            else
            {
                VerifyCall(ODX, 0, nameof(IStandardToken256.TransferTo), transferParams, Times.Never);
                VerifyLog(new CollectMiningRewardsLog { Miner = It.IsAny<Address>(), Amount = It.IsAny<UInt256>() }, Times.Never);
            }
        }
        
        [Fact]
        public void Collect_Throws_Locked()
        {
            var miningPool = CreateNewMiningPool(100);
            
            PersistentState.SetBool(nameof(IOpdexMiningPool.Locked), true);

            miningPool.Invoking(s => s.Collect())
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: LOCKED");
        }
        
        [Theory]
        [InlineData(100, 200, 150, 101, 25_000_000_000, 100_000_000, 100_000_000, 19_600_000)]
        [InlineData(100, 200, 200, 100, 10_000_000_000, 100_000_000, 100_000_000, 100_000_000)]
        [InlineData(100, 200, 125, 125, 10_000_000_000, 100_000_000, 100_000_000, 0)]
        [InlineData(100, 200, 126, 125, 10_000_000_000, 100_000_000, 100_000_000, 1_000_000)]
        public void Exit_Success(ulong periodStart, ulong periodFinish, ulong currentBlock, ulong lastUpdateBlock,
            UInt256 totalSupply, UInt256 rewardRate, UInt256 minerBalance, UInt256 expectedReward)
        {
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.RewardPerToken), UInt256.Zero);
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.TotalSupply), totalSupply);
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);
            PersistentState.SetUInt256($"Balance:{Miner1}", minerBalance);
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.LastUpdateBlock), lastUpdateBlock);
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodFinish);

            var transferRewardParams = new object[] { Miner1, expectedReward };
            SetupCall(ODX, 0ul, nameof(IStandardToken256.TransferTo), transferRewardParams, TransferResult.Transferred(true));
            
            var transferStakingTokensParams = new object[] { Miner1, minerBalance };
            SetupCall(Pool1, 0ul, nameof(IStandardToken256.TransferTo), transferStakingTokensParams, TransferResult.Transferred(true));
            
            var miningPool = CreateNewMiningPool(periodStart);

            SetupBlock(currentBlock);
            SetupMessage(MiningPool1, Miner1);

            miningPool.Exit();

            miningPool.GetReward(Miner1).Should().Be(UInt256.Zero);
            miningPool.TotalSupply.Should().Be(totalSupply - minerBalance);
            miningPool.GetBalance(Miner1).Should().Be(UInt256.Zero);
            
            VerifyCall(Pool1, 0, nameof(IStandardToken256.TransferTo), transferStakingTokensParams, Times.Once);
            VerifyLog(new StopMiningLog { Miner = Miner1, Amount = minerBalance }, Times.Once);

            if (expectedReward > 0)
            {
                VerifyCall(ODX, 0, nameof(IStandardToken256.TransferTo), transferRewardParams, Times.Once);
                VerifyLog(new CollectMiningRewardsLog { Miner = Miner1, Amount = expectedReward }, Times.Once);
            }
            else
            {
                VerifyCall(ODX, 0, nameof(IStandardToken256.TransferTo), transferRewardParams, Times.Never);
                VerifyLog(new CollectMiningRewardsLog { Miner = It.IsAny<Address>(), Amount = It.IsAny<UInt256>() }, Times.Never);
            }
        }
        
        [Fact]
        public void Exit_Throws_Locked()
        {
            var miningPool = CreateNewMiningPool(100);
            
            PersistentState.SetBool(nameof(IOpdexMiningPool.Locked), true);

            miningPool.Invoking(s => s.Exit())
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: LOCKED");
        }

        [Fact]
        public void NotifyRewardAmount_NotActive_Success()
        {
            const ulong block = 100;
            const ulong duration = 100;
            const ulong expectedMiningPeriodEndBlock = block + duration;
            UInt256 rewardAmount = 100_000;
            UInt256 expectedRewardRate = 1_000;
            UInt256 rewardRate = 1_000;
            
            var miningPool = CreateNewMiningPool();
            
            SetupBlock(block);
            SetupMessage(MiningPool1, MiningGovernance);
            
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.MiningDuration), duration);
            PersistentState.SetAddress(nameof(IOpdexMiningPool.MinedToken), ODX);
            PersistentState.SetAddress(nameof(IOpdexMiningPool.MiningGovernance), MiningGovernance);

            var balanceParams = new object[] {MiningPool1};
            SetupCall(ODX, 0ul, nameof(IStandardToken.GetBalance), balanceParams, TransferResult.Transferred(rewardAmount));
            
            miningPool.NotifyRewardAmount(rewardAmount);

            miningPool.RewardRate.Should().Be(expectedRewardRate);
            miningPool.MiningPeriodEndBlock.Should().Be(block + duration);

            VerifyLog(new EnableMiningLog { Amount = rewardAmount, RewardRate = rewardRate, MiningPeriodEndBlock = expectedMiningPeriodEndBlock}, Times.Once);
        }

        [Fact]
        public void NotifyRewardAmount_Active_Success()
        {
            UInt256 rewardAmount = 100_000;
            UInt256 balance = 150_000;
            UInt256 rewardRate = 1_000;
            UInt256 newRewardRate = 1_500;
            const ulong duration = 100;
            const ulong startingBlock = 100;
            const ulong currentBlock = 150;
            const ulong endBlock = 200;

            var miningPool = CreateNewMiningPool(startingBlock);
            
            SetupBlock(currentBlock);
            SetupMessage(MiningPool1, MiningGovernance);
            
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.MiningDuration), duration);
            PersistentState.SetAddress(nameof(IOpdexMiningPool.MinedToken), ODX);
            PersistentState.SetAddress(nameof(IOpdexMiningPool.MiningGovernance), MiningGovernance);
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), endBlock);
            PersistentState.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);

            var balanceParams = new object[] {MiningPool1};
            SetupCall(ODX, 0ul, nameof(IStandardToken.GetBalance), balanceParams, TransferResult.Transferred(balance));
            
            miningPool.NotifyRewardAmount(rewardAmount);

            miningPool.RewardRate.Should().Be(newRewardRate);
            miningPool.LastUpdateBlock.Should().Be(currentBlock);
            miningPool.MiningPeriodEndBlock.Should().Be(currentBlock + duration);

            VerifyLog(new EnableMiningLog { Amount = rewardAmount, RewardRate = newRewardRate, MiningPeriodEndBlock = currentBlock + duration }, Times.Once);
        }

        [Fact]
        public void NotifyRewardAmount_Throws_Locked()
        {
            var miningPool = CreateNewMiningPool();
            
            SetupBlock(100);
            SetupMessage(MiningPool1, MiningGovernance);
            
            PersistentState.SetBool(nameof(IOpdexMiningPool.Locked), true);

            miningPool
                .Invoking(m => m.NotifyRewardAmount(100_000))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: LOCKED");
        }
        
        [Fact]
        public void NotifyRewardAmount_Throws_Unauthorized()
        {
            var miningPool = CreateNewMiningPool();
            
            SetupBlock(100);
            SetupMessage(MiningPool1, Pool1);
            
            PersistentState.SetAddress(nameof(IOpdexMiningPool.MiningGovernance), MiningGovernance);

            miningPool
                .Invoking(m => m.NotifyRewardAmount(100_000))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }

        [Fact]
        public void NotifyRewardAmount_Throws_InvalidBalance()
        {
            const ulong duration = 100;
            
            var miningPool = CreateNewMiningPool();
            
            SetupBlock(100);
            SetupMessage(MiningPool1, MiningGovernance);
            
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.MiningDuration), duration);
            PersistentState.SetAddress(nameof(IOpdexMiningPool.MinedToken), ODX);
            PersistentState.SetAddress(nameof(IOpdexMiningPool.MiningGovernance), MiningGovernance);

            var balanceParams = new object[] {MiningPool1};
            SetupCall(ODX, 0ul, nameof(IStandardToken.GetBalance), balanceParams, TransferResult.Transferred(UInt256.Zero));
            
            miningPool
                .Invoking(m => m.NotifyRewardAmount(100_000))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_BALANCE");
        }
        
        [Fact]
        public void NotifyRewardAmount_Throws_RewardTooHigh()
        {
            const ulong duration = 100;
            UInt256 balance = 1;
            
            var miningPool = CreateNewMiningPool();
            
            SetupBlock(100);
            SetupMessage(MiningPool1, MiningGovernance);
            
            PersistentState.SetUInt64(nameof(IOpdexMiningPool.MiningDuration), duration);
            PersistentState.SetAddress(nameof(IOpdexMiningPool.MinedToken), ODX);
            PersistentState.SetAddress(nameof(IOpdexMiningPool.MiningGovernance), MiningGovernance);

            var balanceParams = new object[] {MiningPool1};
            SetupCall(ODX, 0ul, nameof(IStandardToken.GetBalance), balanceParams, TransferResult.Transferred(balance));
            
            miningPool
                .Invoking(m => m.NotifyRewardAmount(UInt256.MaxValue))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: PROVIDED_REWARD_TOO_HIGH");
        }
    }
}