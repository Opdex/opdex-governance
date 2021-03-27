using System.Linq;
using FluentAssertions;
using Moq;
using OpdexTokenTests.Base;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace OpdexTokenTests
{
    public class MiningGovernanceTests : TestBase
    {
        [Fact]
        public void CreateMiningGovernanceContract_Success()
        {
            const ulong genesis = 100;
            var gov = CreateNewOpdexMiningGovernance(genesis);

            gov.MinedToken.Should().Be(OPDX);
            // 1_971_000 bpy / 12 mo / 4 w + genesis
            gov.NominationPeriodEnd.Should().Be(41062 + genesis);
            gov.MiningPoolsFunded.Should().Be(0u);
            gov.MiningPoolReward.Should().Be(UInt256.Zero);
            gov.Nominations.Should().BeNullOrEmpty();
        }

        [Fact]
        public void GetMiningPool_Success()
        {
            const ulong genesis = 100;
            var gov = CreateNewOpdexMiningGovernance(genesis);
            
            PersistentState.SetAddress($"MiningPool:{OPDX}", Miner1);

            gov.GetMiningPool(OPDX).Should().Be(Miner1);
        }

        [Fact]
        public void NotifyDistribution_SetStakingTokens_Success()
        {
            // Arrange
            UInt256 expectedBalance = 28_000_000_000_000_000;
            UInt256 expectedMiningPoolReward = 583_333_333_333_333;
            var pools = new[] { Pool1, Pool2, Pool3, Pool4 };
            var gov = CreateNewOpdexMiningGovernance();
            
            var miningPool1Params = new object[] {MiningGovernance, OPDX, Pool1};
            var miningPool2Params = new object[] {MiningGovernance, OPDX, Pool2};
            var miningPool3Params = new object[] {MiningGovernance, OPDX, Pool3};
            var miningPool4Params = new object[] {MiningGovernance, OPDX, Pool4};

            SetupCreate<MiningPool>(CreateResult.Succeeded(MiningPool1), 0ul, miningPool1Params);
            SetupCreate<MiningPool>(CreateResult.Succeeded(MiningPool2), 0ul, miningPool2Params);
            SetupCreate<MiningPool>(CreateResult.Succeeded(MiningPool3), 0ul, miningPool3Params);
            SetupCreate<MiningPool>(CreateResult.Succeeded(MiningPool4), 0ul, miningPool4Params);

            var getBalanceParams = new object[] {MiningGovernance};
            SetupCall(OPDX, 0ul, nameof(OpdexToken.GetBalance), getBalanceParams, TransferResult.Transferred(expectedBalance));
            
            SetupMessage(MiningGovernance, OPDX);
            
            // Act
            gov.NotifyDistribution(Serializer.Serialize(pools));

            gov.MiningPoolReward.Should().Be(expectedMiningPoolReward);
            gov.MiningPoolsFunded.Should().Be(0);
            gov.Distributed.Should().BeFalse();
            gov.Nominations.Length.Should().Be(pools.Length);
            foreach (var govNomination in gov.Nominations)
            {
                pools.ToList().Any(p => p == govNomination.StakingPool).Should().BeTrue();
            }

            // Assert
            VerifyCreate<MiningPool>(0ul, miningPool1Params, Times.Once);
            VerifyLog(new MiningPoolCreatedEvent {MiningPool = MiningPool1, StakingPool = Pool1}, Times.Once);
            
            VerifyCreate<MiningPool>(0ul, miningPool2Params, Times.Once);
            VerifyLog(new MiningPoolCreatedEvent {MiningPool = MiningPool2, StakingPool = Pool2}, Times.Once);
            
            VerifyCreate<MiningPool>(0ul, miningPool3Params, Times.Once);
            VerifyLog(new MiningPoolCreatedEvent {MiningPool = MiningPool3, StakingPool = Pool3}, Times.Once);
            
            VerifyCreate<MiningPool>(0ul, miningPool4Params, Times.Once);
            VerifyLog(new MiningPoolCreatedEvent {MiningPool = MiningPool4, StakingPool = Pool4}, Times.Once);
            
            VerifyCall(OPDX, 0ul, nameof(OpdexToken.GetBalance), getBalanceParams, Times.Once);
        }

        [Fact]
        public void NotifyDistribution_Success()
        {
            var gov = CreateNewOpdexMiningGovernance();
            
            SetupMessage(MiningGovernance, OPDX);
            
            gov.NotifyDistribution(new byte[0]);

            gov.Distributed.Should().BeTrue();
        }
    }
}