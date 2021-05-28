using System.Linq;
using FluentAssertions;
using Moq;
using OpdexGovernanceTests.Base;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core;
using Xunit;
using Xunit.Abstractions;

namespace OpdexGovernanceTests
{
    public class OpdexMiningGovernanceTests : TestBase
    {
        private const string NotifyRewardAmountMethod = "NotifyRewardAmount";
        private const string GetMiningPoolMethod = "get_MiningPool";
        private readonly ITestOutputHelper _testOutputHelper;

        public OpdexMiningGovernanceTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void CreateMiningGovernanceContract_Success()
        {
            const ulong genesis = 100;
            var gov = CreateNewOpdexMiningGovernance(genesis);

            gov.MinedToken.Should().Be(ODX);
            gov.NominationPeriodEnd.Should().Be(0);
            gov.MiningPoolsFunded.Should().Be(0u);
            gov.MiningPoolReward.Should().Be(UInt256.Zero);
            gov.Nominations.Should().BeNull();
            gov.MiningDuration.Should().Be(BlocksPerMonth);
        }

        [Fact]
        public void GetMiningPool_Success()
        {
            const ulong genesis = 100;
            var gov = CreateNewOpdexMiningGovernance(genesis);
            
            PersistentState.SetAddress($"MiningPool:{ODX}", Miner);

            gov.GetMiningPool(ODX).Should().Be(Miner);
        }
        
        #region Notify Distribution Tests

        [Fact]
        public void NotifyDistribution_SetStakingTokens_Success()
        {
            // Arrange
            UInt256 expectedBalance = 28_000_000_000_000_000; // 280 million
            UInt256 expectedMiningPoolReward = 583_333_333_333_333; // 5,8333,333 million
            var pools = new[] { Pool1, Pool2, Pool3, Pool4 };
            var gov = CreateNewOpdexMiningGovernance();

            var getBalanceParams = new object[] {MiningGovernance};
            SetupCall(ODX, 0ul, nameof(IOpdexMinedToken.GetBalance), getBalanceParams, TransferResult.Transferred(expectedBalance));

            SetupCall(Pool1, 0, GetMiningPoolMethod, null, TransferResult.Transferred(MiningPool1));
            SetupCall(Pool2, 0, GetMiningPoolMethod, null, TransferResult.Transferred(MiningPool2));
            SetupCall(Pool3, 0, GetMiningPoolMethod, null, TransferResult.Transferred(MiningPool3));
            SetupCall(Pool4, 0, GetMiningPoolMethod, null, TransferResult.Transferred(MiningPool4));
            
            SetupMessage(MiningGovernance, ODX);
            
            // Act
            gov.NotifyDistribution(Serializer.Serialize(pools));

            gov.MiningPoolReward.Should().Be(expectedMiningPoolReward);
            gov.MiningPoolsFunded.Should().Be(0);
            gov.Notified.Should().BeFalse();
            gov.Nominations.Length.Should().Be(pools.Length);
            foreach (var govNomination in gov.Nominations)
            {
                pools.ToList().Any(p => p == govNomination.StakingPool).Should().BeTrue();
            }
            
            VerifyCall(Pool1, 0, GetMiningPoolMethod, null, Times.Once);
            VerifyCall(Pool2, 0, GetMiningPoolMethod, null, Times.Once);
            VerifyCall(Pool3, 0, GetMiningPoolMethod, null, Times.Once);
            VerifyCall(Pool4, 0, GetMiningPoolMethod, null, Times.Once);
            
            VerifyCall(ODX, 0ul, nameof(IOpdexMinedToken.GetBalance), getBalanceParams, Times.Once);
        }

        [Fact]
        public void NotifyDistribution_Success()
        {
            var gov = CreateNewOpdexMiningGovernance();
            
            SetupMessage(MiningGovernance, ODX);
            
            gov.NotifyDistribution(new byte[0]);

            gov.Notified.Should().BeTrue();
        }
        
        [Fact]
        public void NotifyDistribution_Success_Locked()
        {
            var gov = CreateNewOpdexMiningGovernance();

            SetupMessage(MiningGovernance, ODX);
            
            PersistentState.SetBool(nameof(IOpdexMiningGovernance.Locked), true);
            
            gov
                .Invoking(g => g.NotifyDistribution(new byte[0]))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: LOCKED");
        }
        
        [Fact]
        public void NotifyDistribution_Throws_InvalidSender()
        {
            var gov = CreateNewOpdexMiningGovernance();

            SetupMessage(MiningGovernance, Miner);
            
            gov
                .Invoking(g => g.NotifyDistribution(new byte[0]))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_SENDER");
        }
        
        #endregion
        
        #region Nominite Liquidity Pool Tests
        
        [Fact]
        public void NominateLiquidityPool_ExistingNominationUpdate_Success()
        {
            var nominatedLiquidityPool = Pool3;
            var nominatedMiningPool = MiningPool3;
            UInt256 nominationWeight = 100_000_000;
            const int expectedAffectedIndex = 2;
            
            var nominations = new[]
            {
                new Nomination {StakingPool = Pool1, Weight = 50_000_000},
                new Nomination {StakingPool = Pool2, Weight = 150_000_000},
                new Nomination {StakingPool = Pool3, Weight = 90_000_000},
                new Nomination {StakingPool = Pool4, Weight = 200_000_000}
            };
            
            var gov = CreateNewOpdexMiningGovernance();

            SetupMessage(MiningGovernance, ODX);

            PersistentState.SetArray(nameof(IOpdexMiningGovernance.Nominations), nominations);
            PersistentState.SetUInt64(nameof(IOpdexMiningGovernance.NominationPeriodEnd), 100_000);
            PersistentState.SetAddress($"MiningPool:{nominatedLiquidityPool}", nominatedMiningPool);
            
            gov.NominateLiquidityPool(nominatedLiquidityPool, nominationWeight);
            gov.Nominations.Length.Should().Be(4);
            
            for (var i = 0; i < gov.Nominations.Length; i++)
            {
                var expectedWeight = i == expectedAffectedIndex ? nominationWeight : nominations[i].Weight;
                var govNomination = gov.Nominations[i];
                govNomination.StakingPool.Should().Be(nominations[i].StakingPool);
                govNomination.Weight.Should().Be(expectedWeight);
            }

            VerifyLog(new NominationLog
            {
                StakingPool = nominatedLiquidityPool,
                Weight = nominationWeight,
                MiningPool = nominatedMiningPool
            }, Times.Once);
        }
        
        [Fact]
        public void NominateLiquidityPool_Does_QualifyAgainstExistingNominations_Success()
        {
            var nominatedLiquidityPool = Pool5;
            var nominationMiningPool = MiningPool5;
            UInt256 nominationWeight = 400_000_000;
            const int expectedAffectedIndex = 1;
            
            var nominations = new[]
            {
                new Nomination {StakingPool = Pool1, Weight = 300_000_000},
                new Nomination {StakingPool = Pool2, Weight = 50_000_000},
                new Nomination {StakingPool = Pool3, Weight = 190_000_000},
                new Nomination {StakingPool = Pool4, Weight = 200_000_000}
            };
            
            var gov = CreateNewOpdexMiningGovernance();

            SetupMessage(MiningGovernance, ODX);

            SetupCall(nominatedLiquidityPool, 0, GetMiningPoolMethod, null, TransferResult.Transferred(nominationMiningPool));

            PersistentState.SetArray(nameof(IOpdexMiningGovernance.Nominations), nominations);
            PersistentState.SetUInt64(nameof(IOpdexMiningGovernance.NominationPeriodEnd), 100_000);
            
            gov.NominateLiquidityPool(nominatedLiquidityPool, nominationWeight);
            gov.Nominations.Length.Should().Be(4);
            
            for (var i = 0; i < gov.Nominations.Length; i++)
            {
                var expectedWeight = i == expectedAffectedIndex ? nominationWeight : nominations[i].Weight;
                var expectedPool = i == expectedAffectedIndex ? nominatedLiquidityPool : nominations[i].StakingPool;
                
                var govNomination = gov.Nominations[i];
                govNomination.StakingPool.Should().Be(expectedPool);
                govNomination.Weight.Should().Be(expectedWeight);
            }
            
            VerifyCall(nominatedLiquidityPool, 0, GetMiningPoolMethod, null, Times.Once);

            VerifyLog(new NominationLog
            {
                StakingPool = nominatedLiquidityPool,
                Weight = nominationWeight,
                MiningPool = nominationMiningPool
            }, Times.Once);
        }
        
        [Fact]
        public void NominateLiquidityPool_DoesNot_QualifyAgainstExistingNominations_Success()
        {
            var nominations = new[]
            {
                new Nomination {StakingPool = Pool1, Weight = 300_000_000},
                new Nomination {StakingPool = Pool2, Weight = 150_000_000},
                new Nomination {StakingPool = Pool3, Weight = 190_000_000},
                new Nomination {StakingPool = Pool4, Weight = 200_000_000}
            };
            
            var gov = CreateNewOpdexMiningGovernance();

            SetupMessage(MiningGovernance, ODX);

            PersistentState.SetArray(nameof(IOpdexMiningGovernance.Nominations), nominations);
            PersistentState.SetUInt64(nameof(IOpdexMiningGovernance.NominationPeriodEnd), 100_000);
            
            gov.NominateLiquidityPool(Pool5, 150_000_000);

            for (var i = 0; i < nominations.Length; i++)
            {
                gov.Nominations[i].StakingPool.Should().Be(nominations[i].StakingPool);
                gov.Nominations[i].Weight.Should().Be(nominations[i].Weight);
            }

            VerifyLog(new NominationLog
            {
                StakingPool = It.IsAny<Address>(),
                Weight = It.IsAny<UInt256>(),
                MiningPool = It.IsAny<Address>()
            }, Times.Never);
        }

        [Fact]
        public void NominateLiquidityPool_Throws_InvalidSender()
        {
            UInt256 nominationWeight = 100_000;
            
            var gov = CreateNewOpdexMiningGovernance();

            SetupMessage(MiningGovernance, Miner);
            
            gov
                .Invoking(g => g.NominateLiquidityPool(Pool5, nominationWeight))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_SENDER");
        }
        
        [Fact]
        public void NominateLiquidityPool_Throws_InvalidMiningPool()
        {
            var nominatedLiquidityPool = Pool5;
            UInt256 nominationWeight = 400_000_000;
            
            var nominations = new[]
            {
                new Nomination {StakingPool = Pool1, Weight = 300_000_000},
                new Nomination {StakingPool = Pool2, Weight = 50_000_000},
                new Nomination {StakingPool = Pool3, Weight = 190_000_000},
                new Nomination {StakingPool = Pool4, Weight = 200_000_000}
            };
            
            var gov = CreateNewOpdexMiningGovernance();

            SetupMessage(MiningGovernance, ODX);
            
            PersistentState.SetArray(nameof(IOpdexMiningGovernance.Nominations), nominations);
            PersistentState.SetUInt64(nameof(IOpdexMiningGovernance.NominationPeriodEnd), 100_000);
            
            SetupCall(nominatedLiquidityPool, 0, GetMiningPoolMethod, null, TransferResult.Transferred(Address.Zero));

            gov
                .Invoking(g => g.NominateLiquidityPool(nominatedLiquidityPool, nominationWeight))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_MINING_POOL");
        }
        
        #endregion
        
        #region Notify Mining Pools

        [Fact]
        public void RewardMiningPools_All_Success()
        {
            UInt256 miningPoolReward = 100_000_000;
            const ulong currentBlock = 100_001;
            UInt256 expectedNominationWeight = 1;
            
            var gov = CreateNewOpdexMiningGovernance();
            var nominations = new[]
            {
                new Nomination {StakingPool = Pool1, Weight = 300_000_000},
                new Nomination {StakingPool = Pool2, Weight = 150_000_000},
                new Nomination {StakingPool = Pool3, Weight = 190_000_000},
                new Nomination {StakingPool = Pool4, Weight = 200_000_000}
            };

            SetupMessage(MiningGovernance, Miner);
            SetupBlock(currentBlock);

            var transferToPool1Params = new object[] { MiningPool1, miningPoolReward };
            SetupCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToPool1Params, TransferResult.Transferred(true));
            
            var transferToPool2Params = new object[] { MiningPool2, miningPoolReward };
            SetupCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToPool2Params, TransferResult.Transferred(true));
            
            var transferToPool3Params = new object[] { MiningPool3, miningPoolReward };
            SetupCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToPool3Params, TransferResult.Transferred(true));
            
            var transferToPool4Params = new object[] { MiningPool4, miningPoolReward };
            SetupCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToPool4Params, TransferResult.Transferred(true));

            var notifyParams = new object[] {miningPoolReward};

            SetupCall(MiningPool1, 0ul, NotifyRewardAmountMethod, notifyParams, TransferResult.Transferred(null));
            SetupCall(MiningPool2, 0ul, NotifyRewardAmountMethod, notifyParams, TransferResult.Transferred(null));
            SetupCall(MiningPool3, 0ul, NotifyRewardAmountMethod, notifyParams, TransferResult.Transferred(null));
            SetupCall(MiningPool4, 0ul, NotifyRewardAmountMethod, notifyParams, TransferResult.Transferred(null));
            
            PersistentState.SetArray(nameof(IOpdexMiningGovernance.Nominations), nominations);
            PersistentState.SetUInt256(nameof(IOpdexMiningGovernance.MiningPoolReward), miningPoolReward);
            PersistentState.SetAddress($"MiningPool:{Pool1}", MiningPool1);
            PersistentState.SetAddress($"MiningPool:{Pool2}", MiningPool2);
            PersistentState.SetAddress($"MiningPool:{Pool3}", MiningPool3);
            PersistentState.SetAddress($"MiningPool:{Pool4}", MiningPool4);

            gov.RewardMiningPools();
            gov.MiningPoolsFunded.Should().Be(4);
            gov.Nominations.Length.Should().Be(4);
            gov.NominationPeriodEnd.Should().Be(currentBlock + BlocksPerMonth);
            
            for (var i = 0; i < gov.Nominations.Length; i++)
            {
                var govNomination = gov.Nominations[i];
                govNomination.StakingPool.Should().Be(nominations[i].StakingPool);
                govNomination.Weight.Should().Be(expectedNominationWeight);
            }

            VerifyCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToPool1Params, Times.AtLeastOnce);
            VerifyCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToPool2Params, Times.AtLeastOnce);
            VerifyCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToPool3Params, Times.AtLeastOnce);
            VerifyCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToPool4Params, Times.AtLeastOnce);
            
            VerifyCall(MiningPool1, 0ul, NotifyRewardAmountMethod, notifyParams, Times.Once);
            VerifyCall(MiningPool2, 0ul, NotifyRewardAmountMethod, notifyParams, Times.Once);
            VerifyCall(MiningPool3, 0ul, NotifyRewardAmountMethod, notifyParams, Times.Once);
            VerifyCall(MiningPool4, 0ul, NotifyRewardAmountMethod, notifyParams, Times.Once);

            VerifyLog(new RewardMiningPoolLog { StakingPool = Pool1, MiningPool = MiningPool1, Amount = miningPoolReward }, Times.Once);
            VerifyLog(new RewardMiningPoolLog { StakingPool = Pool2, MiningPool = MiningPool2, Amount = miningPoolReward }, Times.Once);
            VerifyLog(new RewardMiningPoolLog { StakingPool = Pool3, MiningPool = MiningPool3, Amount = miningPoolReward }, Times.Once);
            VerifyLog(new RewardMiningPoolLog { StakingPool = Pool4, MiningPool = MiningPool4, Amount = miningPoolReward }, Times.Once);
        }
        
        [Fact]
        public void RewardMiningPools_Remaining_Success()
        {
            UInt256 miningPoolReward = 100_000_000;
            const ulong currentBlock = 100_001;
            UInt256 expectedNominationWeight = 1;
            
            var gov = CreateNewOpdexMiningGovernance();
            var nominations = new[]
            {
                new Nomination {StakingPool = Pool1, Weight = UInt256.Zero},
                new Nomination {StakingPool = Pool2, Weight = 150_000_000},
                new Nomination {StakingPool = Pool3, Weight = 190_000_000},
                new Nomination {StakingPool = Pool4, Weight = 200_000_000}
            };

            SetupMessage(MiningGovernance, Miner);
            SetupBlock(currentBlock);

            var transferToPool2Params = new object[] { MiningPool2, miningPoolReward };
            SetupCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToPool2Params, TransferResult.Transferred(true));
            
            var transferToPool3Params = new object[] { MiningPool3, miningPoolReward };
            SetupCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToPool3Params, TransferResult.Transferred(true));
            
            var transferToPool4Params = new object[] { MiningPool4, miningPoolReward };
            SetupCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToPool4Params, TransferResult.Transferred(true));

            var notifyParams = new object[] {miningPoolReward};

            SetupCall(MiningPool2, 0ul, NotifyRewardAmountMethod, notifyParams, TransferResult.Transferred(null));
            SetupCall(MiningPool3, 0ul, NotifyRewardAmountMethod, notifyParams, TransferResult.Transferred(null));
            SetupCall(MiningPool4, 0ul, NotifyRewardAmountMethod, notifyParams, TransferResult.Transferred(null));
            
            PersistentState.SetArray(nameof(IOpdexMiningGovernance.Nominations), nominations);
            PersistentState.SetUInt256(nameof(IOpdexMiningGovernance.MiningPoolReward), miningPoolReward);
            PersistentState.SetUInt32(nameof(IOpdexMiningGovernance.MiningPoolsFunded), 1);
            PersistentState.SetAddress($"MiningPool:{Pool1}", MiningPool1);
            PersistentState.SetAddress($"MiningPool:{Pool2}", MiningPool2);
            PersistentState.SetAddress($"MiningPool:{Pool3}", MiningPool3);
            PersistentState.SetAddress($"MiningPool:{Pool4}", MiningPool4);

            gov.MiningPoolsFunded.Should().Be(1);
            gov.RewardMiningPools();
            gov.MiningPoolsFunded.Should().Be(4);
            gov.Nominations.Length.Should().Be(4);
            gov.NominationPeriodEnd.Should().Be(currentBlock + BlocksPerMonth);
            
            for (var i = 0; i < gov.Nominations.Length; i++)
            {
                var govNomination = gov.Nominations[i];
                govNomination.StakingPool.Should().Be(nominations[i].StakingPool);
                govNomination.Weight.Should().Be(expectedNominationWeight);
            }

            VerifyCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToPool2Params, Times.AtLeastOnce);
            VerifyCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToPool3Params, Times.AtLeastOnce);
            VerifyCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToPool4Params, Times.AtLeastOnce);
            
            VerifyCall(MiningPool1, 0ul, NotifyRewardAmountMethod, notifyParams, Times.Never);
            VerifyCall(MiningPool2, 0ul, NotifyRewardAmountMethod, notifyParams, Times.Once);
            VerifyCall(MiningPool3, 0ul, NotifyRewardAmountMethod, notifyParams, Times.Once);
            VerifyCall(MiningPool4, 0ul, NotifyRewardAmountMethod, notifyParams, Times.Once);
            
            VerifyLog(new RewardMiningPoolLog { StakingPool = Pool2, MiningPool = MiningPool2, Amount = miningPoolReward }, Times.Once);
            VerifyLog(new RewardMiningPoolLog { StakingPool = Pool3, MiningPool = MiningPool3, Amount = miningPoolReward }, Times.Once);
            VerifyLog(new RewardMiningPoolLog { StakingPool = Pool4, MiningPool = MiningPool4, Amount = miningPoolReward }, Times.Once);
        }
        
        [Fact]
        public void RewardMiningPools_All_ResetYearly_Success()
        {
            UInt256 miningPoolReward = 100_000_000;
            const ulong currentBlock = 100_001;
            const bool notified = true;
            const uint miningPoolsPerYear = 48;
            UInt256 expectedNominationWeight = 1;
            
            var gov = CreateNewOpdexMiningGovernance();
            var nominations = new[]
            {
                new Nomination {StakingPool = Pool1, Weight = 300_000_000},
                new Nomination {StakingPool = Pool2, Weight = 150_000_000},
                new Nomination {StakingPool = Pool3, Weight = 190_000_000},
                new Nomination {StakingPool = Pool4, Weight = 200_000_000}
            };

            SetupMessage(MiningGovernance, Miner);
            SetupBlock(currentBlock);

            var transferToPool1Params = new object[] { MiningPool1, miningPoolReward };
            SetupCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToPool1Params, TransferResult.Transferred(true));
            
            var transferToPool2Params = new object[] { MiningPool2, miningPoolReward };
            SetupCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToPool2Params, TransferResult.Transferred(true));
            
            var transferToPool3Params = new object[] { MiningPool3, miningPoolReward };
            SetupCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToPool3Params, TransferResult.Transferred(true));
            
            var transferToPool4Params = new object[] { MiningPool4, miningPoolReward };
            SetupCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToPool4Params, TransferResult.Transferred(true));

            var notifyParams = new object[] {miningPoolReward};
            SetupCall(MiningPool1, 0ul, NotifyRewardAmountMethod, notifyParams, TransferResult.Transferred(null));
            SetupCall(MiningPool2, 0ul, NotifyRewardAmountMethod, notifyParams, TransferResult.Transferred(null));
            SetupCall(MiningPool3, 0ul, NotifyRewardAmountMethod, notifyParams, TransferResult.Transferred(null));
            SetupCall(MiningPool4, 0ul, NotifyRewardAmountMethod, notifyParams, TransferResult.Transferred(null));
            
            // Get Balance of Mining Governance
            var getBalanceParams = new object[] { MiningGovernance };
            SetupCall(ODX, 0ul, nameof(IOpdexMinedToken.GetBalance), getBalanceParams, TransferResult.Transferred(miningPoolReward * 2 * miningPoolsPerYear));
            
            PersistentState.SetArray(nameof(IOpdexMiningGovernance.Nominations), nominations);
            PersistentState.SetUInt256(nameof(IOpdexMiningGovernance.MiningPoolReward), miningPoolReward);
            PersistentState.SetBool(nameof(IOpdexMiningGovernance.Notified), notified);
            PersistentState.SetUInt32(nameof(IOpdexMiningGovernance.MiningPoolsFunded), 44);
            PersistentState.SetAddress($"MiningPool:{Pool1}", MiningPool1);
            PersistentState.SetAddress($"MiningPool:{Pool2}", MiningPool2);
            PersistentState.SetAddress($"MiningPool:{Pool3}", MiningPool3);
            PersistentState.SetAddress($"MiningPool:{Pool4}", MiningPool4);

            gov.RewardMiningPools();
            gov.MiningPoolsFunded.Should().Be(0);
            gov.Nominations.Length.Should().Be(4);
            gov.NominationPeriodEnd.Should().Be(currentBlock + BlocksPerMonth);
            gov.Notified.Should().BeFalse();
            gov.MiningPoolReward.Should().Be(miningPoolReward * 2);

            for (var i = 0; i < gov.Nominations.Length; i++)
            {
                var govNomination = gov.Nominations[i];
                govNomination.StakingPool.Should().Be(nominations[i].StakingPool);
                govNomination.Weight.Should().Be(expectedNominationWeight);
            }

            VerifyCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToPool1Params, Times.AtLeastOnce);
            VerifyCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToPool2Params, Times.AtLeastOnce);
            VerifyCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToPool3Params, Times.AtLeastOnce);
            VerifyCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToPool4Params, Times.AtLeastOnce);
            
            VerifyCall(MiningPool1, 0ul, NotifyRewardAmountMethod, notifyParams, Times.Once);
            VerifyCall(MiningPool2, 0ul, NotifyRewardAmountMethod, notifyParams, Times.Once);
            VerifyCall(MiningPool3, 0ul, NotifyRewardAmountMethod, notifyParams, Times.Once);
            VerifyCall(MiningPool4, 0ul, NotifyRewardAmountMethod, notifyParams, Times.Once);
            
            VerifyCall(ODX, 0ul, nameof(IOpdexMinedToken.GetBalance), getBalanceParams, Times.Once);

            VerifyLog(new RewardMiningPoolLog { StakingPool = Pool1, MiningPool = MiningPool1, Amount = miningPoolReward }, Times.Once);
            VerifyLog(new RewardMiningPoolLog { StakingPool = Pool2, MiningPool = MiningPool2, Amount = miningPoolReward }, Times.Once);
            VerifyLog(new RewardMiningPoolLog { StakingPool = Pool3, MiningPool = MiningPool3, Amount = miningPoolReward }, Times.Once);
            VerifyLog(new RewardMiningPoolLog { StakingPool = Pool4, MiningPool = MiningPool4, Amount = miningPoolReward }, Times.Once);
        }
        
        #endregion

        #region Notify Mining Pool

        [Fact]
        public void RewardMiningPool_NotLast_Success()
        {
            UInt256 miningPoolReward = 100_000_000;
            var gov = CreateNewOpdexMiningGovernance();
            var nominations = new[]
            {
                new Nomination {StakingPool = Pool1, Weight = 300_000_000},
                new Nomination {StakingPool = Pool2, Weight = 150_000_000},
                new Nomination {StakingPool = Pool3, Weight = 190_000_000},
                new Nomination {StakingPool = Pool4, Weight = 200_000_000}
            };

            SetupMessage(MiningGovernance, Miner);
            SetupBlock(ulong.MaxValue);

            var transferToParams = new object[] { MiningPool1, miningPoolReward };
            SetupCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToParams, TransferResult.Transferred(true));

            var notifyParams = new object[] {miningPoolReward};
            SetupCall(MiningPool1, 0ul, NotifyRewardAmountMethod, notifyParams, TransferResult.Transferred(null));
            
            PersistentState.SetArray(nameof(IOpdexMiningGovernance.Nominations), nominations);
            PersistentState.SetUInt256(nameof(IOpdexMiningGovernance.MiningPoolReward), miningPoolReward);
            PersistentState.SetAddress($"MiningPool:{Pool1}", MiningPool1);

            gov.RewardMiningPool();
            gov.MiningPoolsFunded.Should().Be(1);
            gov.Nominations[0].StakingPool.Should().Be(Pool1);
            gov.Nominations[0].Weight.Should().Be(UInt256.Zero);
            gov.Nominations.Length.Should().Be(4);
            
            for (var i = 0; i < gov.Nominations.Length; i++)
            {
                var expectedWeight = i == 0 ? UInt256.Zero : nominations[i].Weight;
                var govNomination = gov.Nominations[i];
                govNomination.StakingPool.Should().Be(nominations[i].StakingPool);
                govNomination.Weight.Should().Be(expectedWeight);
            }

            VerifyCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToParams, Times.Once);
            VerifyCall(MiningPool1, 0ul, NotifyRewardAmountMethod, notifyParams, Times.Once);

            VerifyLog(new RewardMiningPoolLog
            {
                StakingPool = Pool1,
                MiningPool = MiningPool1,
                Amount = miningPoolReward
            }, Times.Once);
        }
        
        [Fact]
        public void RewardMiningPool_Last_Success()
        {
            UInt256 miningPoolReward = 100_000_000;
            const ulong nominationPeriodEnd = 100_000;
            const ulong currentBlock = 100_001;
            UInt256 expectedNominationWeight = 1;

            var gov = CreateNewOpdexMiningGovernance();
            var nominations = new[]
            {
                new Nomination {StakingPool = Pool1, Weight = UInt256.Zero},
                new Nomination {StakingPool = Pool2, Weight = UInt256.Zero},
                new Nomination {StakingPool = Pool3, Weight = UInt256.Zero},
                new Nomination {StakingPool = Pool4, Weight = 200_000_000}
            };

            SetupMessage(MiningGovernance, Miner);
            SetupBlock(currentBlock);

            // Transfer rewards to mining pool
            var transferToParams = new object[] { MiningPool4, miningPoolReward };
            SetupCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToParams, TransferResult.Transferred(true));

            // Notify Mining Pool
            var notifyParams = new object[] {miningPoolReward};
            SetupCall(MiningPool4, 0ul, NotifyRewardAmountMethod, notifyParams, TransferResult.Transferred(null));

            PersistentState.SetArray(nameof(IOpdexMiningGovernance.Nominations), nominations);
            PersistentState.SetUInt256(nameof(IOpdexMiningGovernance.MiningPoolReward), miningPoolReward);
            PersistentState.SetUInt32(nameof(IOpdexMiningGovernance.MiningPoolsFunded), 3);
            PersistentState.SetUInt64(nameof(IOpdexMiningGovernance.NominationPeriodEnd), nominationPeriodEnd);
            PersistentState.SetAddress($"MiningPool:{Pool4}", MiningPool4);

            gov.RewardMiningPool();
            gov.MiningPoolsFunded.Should().Be(4);
            gov.Nominations.Length.Should().Be(4);
            gov.Notified.Should().BeFalse();
            gov.MiningPoolReward.Should().Be(miningPoolReward);
            
            for (var i = 0; i < gov.Nominations.Length; i++)
            {
                var govNomination = gov.Nominations[i];
                govNomination.StakingPool.Should().Be(nominations[i].StakingPool);
                govNomination.Weight.Should().Be(expectedNominationWeight);
            }

            VerifyCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToParams, Times.Once);
            VerifyCall(MiningPool4, 0ul, NotifyRewardAmountMethod, notifyParams, Times.Once);

            VerifyLog(new RewardMiningPoolLog
            {
                StakingPool = Pool4,
                MiningPool = MiningPool4,
                Amount = miningPoolReward
            }, Times.Once);
        }
        
        [Fact]
        public void RewardMiningPool_Last_ResetYearly_Success()
        {
            UInt256 miningPoolReward = 100_000_000;
            const uint miningPoolsPerYear = 48;
            const bool notified = true;
            const ulong nominationPeriodEnd = 100_000;
            const ulong currentBlock = 100_001;
            UInt256 expectedNominationWeight = 1;

            var gov = CreateNewOpdexMiningGovernance();
            var nominations = new[]
            {
                new Nomination {StakingPool = Pool1, Weight = 0},
                new Nomination {StakingPool = Pool2, Weight = 0},
                new Nomination {StakingPool = Pool3, Weight = 0},
                new Nomination {StakingPool = Pool4, Weight = 200_000_000}
            };

            SetupMessage(MiningGovernance, Miner);
            SetupBlock(currentBlock);

            // Transfer rewards to mining pool
            var transferToParams = new object[] { MiningPool4, miningPoolReward };
            SetupCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToParams, TransferResult.Transferred(true));

            // Notify Mining Pool
            var notifyParams = new object[] {miningPoolReward};
            SetupCall(MiningPool4, 0ul, NotifyRewardAmountMethod, notifyParams, TransferResult.Transferred(null));
            
            // Get Balance of Mining Governance
            var getBalanceParams = new object[] { MiningGovernance };
            SetupCall(ODX, 0ul, nameof(IOpdexMinedToken.GetBalance), getBalanceParams, TransferResult.Transferred(miningPoolReward * 2 * miningPoolsPerYear));

            PersistentState.SetArray(nameof(IOpdexMiningGovernance.Nominations), nominations);
            PersistentState.SetUInt256(nameof(IOpdexMiningGovernance.MiningPoolReward), miningPoolReward);
            PersistentState.SetUInt32(nameof(IOpdexMiningGovernance.MiningPoolsFunded), 47);
            PersistentState.SetUInt64(nameof(IOpdexMiningGovernance.NominationPeriodEnd), nominationPeriodEnd);
            PersistentState.SetBool(nameof(IOpdexMiningGovernance.Notified), notified);
            PersistentState.SetAddress($"MiningPool:{Pool4}", MiningPool4);

            gov.RewardMiningPool();
            gov.MiningPoolsFunded.Should().Be(0);
            gov.Nominations.Length.Should().Be(4);
            gov.NominationPeriodEnd.Should().Be(currentBlock + BlocksPerMonth);
            gov.Notified.Should().BeFalse();
            gov.MiningPoolReward.Should().Be(miningPoolReward * 2);
            
            for (var i = 0; i < gov.Nominations.Length; i++)
            {
                var govNomination = gov.Nominations[i];
                govNomination.StakingPool.Should().Be(nominations[i].StakingPool);
                govNomination.Weight.Should().Be(expectedNominationWeight);
            }

            VerifyCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToParams, Times.Once);
            VerifyCall(MiningPool4, 0ul, NotifyRewardAmountMethod, notifyParams, Times.Once);
            VerifyCall(ODX, 0ul, nameof(IOpdexMinedToken.GetBalance), getBalanceParams, Times.Once);

            VerifyLog(new RewardMiningPoolLog
            {
                StakingPool = Pool4,
                MiningPool = MiningPool4,
                Amount = miningPoolReward
            }, Times.Once);
        }
        
        [Fact]
        public void RewardMiningPool_IgnoreFailed_NotifyRewardAmount_Success()
        {
            UInt256 miningPoolReward = 100_000_000;
            const ulong nominationPeriodEnd = 100_000;
            const ulong currentBlock = 100_001;
            UInt256 expectedNominationWeight = 1;

            var gov = CreateNewOpdexMiningGovernance();
            var nominations = new[]
            {
                new Nomination {StakingPool = Pool1, Weight = 0},
                new Nomination {StakingPool = Pool2, Weight = 0},
                new Nomination {StakingPool = Pool3, Weight = 0},
                new Nomination {StakingPool = Pool4, Weight = 200_000_000}
            };

            SetupMessage(MiningGovernance, Miner);
            SetupBlock(currentBlock);

            // Transfer rewards to mining pool
            var transferToParams = new object[] { MiningPool4, miningPoolReward };
            SetupCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToParams, TransferResult.Transferred(true));

            // Notify Mining Pool
            var notifyParams = new object[] {miningPoolReward};
            SetupCall(MiningPool4, 0ul, NotifyRewardAmountMethod, notifyParams, TransferResult.Failed());

            PersistentState.SetArray(nameof(IOpdexMiningGovernance.Nominations), nominations);
            PersistentState.SetUInt256(nameof(IOpdexMiningGovernance.MiningPoolReward), miningPoolReward);
            PersistentState.SetUInt32(nameof(IOpdexMiningGovernance.MiningPoolsFunded), 15);
            PersistentState.SetUInt64(nameof(IOpdexMiningGovernance.NominationPeriodEnd), nominationPeriodEnd);
            PersistentState.SetAddress($"MiningPool:{Pool4}", MiningPool4);

            gov.RewardMiningPool();
            gov.MiningPoolsFunded.Should().Be(16);
            gov.Nominations.Length.Should().Be(4);
            gov.NominationPeriodEnd.Should().Be(currentBlock + BlocksPerMonth);
            gov.Notified.Should().BeFalse();
            gov.MiningPoolReward.Should().Be(miningPoolReward);
            
            for (var i = 0; i < gov.Nominations.Length; i++)
            {
                var govNomination = gov.Nominations[i];
                govNomination.StakingPool.Should().Be(nominations[i].StakingPool);
                govNomination.Weight.Should().Be(expectedNominationWeight);
            }

            VerifyCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToParams, Times.Once);
            VerifyCall(MiningPool4, 0ul, NotifyRewardAmountMethod, notifyParams, Times.Once);

            VerifyLog(new RewardMiningPoolLog
            {
                StakingPool = Pool4,
                MiningPool = MiningPool4,
                Amount = miningPoolReward
            }, Times.Once);
        }

        [Fact]
        public void RewardMiningPool_Last_Throws_GovernanceAwaitingDistribution()
        {
            UInt256 miningPoolReward = 100_000_000;
            const uint miningPoolsPerYear = 48;
            const bool notified = false;
            const ulong nominationPeriodEnd = 100_000;
            const ulong currentBlock = 100_001;

            var gov = CreateNewOpdexMiningGovernance();
            var nominations = new[]
            {
                new Nomination {StakingPool = Address.Zero, Weight = UInt256.Zero},
                new Nomination {StakingPool = Address.Zero, Weight = UInt256.Zero},
                new Nomination {StakingPool = Address.Zero, Weight = UInt256.Zero},
                new Nomination {StakingPool = Pool4, Weight = 200_000_000}
            };

            SetupMessage(MiningGovernance, Miner);
            SetupBlock(currentBlock);

            // Transfer rewards to mining pool
            var transferToParams = new object[] { MiningPool4, miningPoolReward };
            SetupCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToParams, TransferResult.Transferred(true));

            // Notify Mining Pool
            var notifyParams = new object[] {miningPoolReward};
            SetupCall(MiningPool4, 0ul, NotifyRewardAmountMethod, notifyParams, TransferResult.Transferred(null));
            
            // Get Balance of Mining Governance
            var getBalanceParams = new object[] { MiningGovernance };
            SetupCall(ODX, 0ul, nameof(IOpdexMinedToken.GetBalance), getBalanceParams, TransferResult.Transferred(miningPoolReward * 2 * miningPoolsPerYear));

            PersistentState.SetArray(nameof(IOpdexMiningGovernance.Nominations), nominations);
            PersistentState.SetUInt256(nameof(IOpdexMiningGovernance.MiningPoolReward), miningPoolReward);
            PersistentState.SetUInt32(nameof(IOpdexMiningGovernance.MiningPoolsFunded), 47);
            PersistentState.SetUInt64(nameof(IOpdexMiningGovernance.NominationPeriodEnd), nominationPeriodEnd);
            PersistentState.SetBool(nameof(IOpdexMiningGovernance.Notified), notified);
            PersistentState.SetAddress($"MiningPool:{Pool4}", MiningPool4);

            gov
                .Invoking(g => g.RewardMiningPool())
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: TOKEN_DISTRIBUTION_REQUIRED");
        }
        
        [Fact]
        public void RewardMiningPool_Last_Throws_InvalidBalance()
        {
            UInt256 miningPoolReward = 100_000_000;
            const bool notified = true;
            const ulong nominationPeriodEnd = 100_000;
            const ulong currentBlock = 100_001;

            var gov = CreateNewOpdexMiningGovernance();
            var nominations = new[]
            {
                new Nomination {StakingPool = Address.Zero, Weight = UInt256.Zero},
                new Nomination {StakingPool = Address.Zero, Weight = UInt256.Zero},
                new Nomination {StakingPool = Address.Zero, Weight = UInt256.Zero},
                new Nomination {StakingPool = Pool4, Weight = 200_000_000}
            };

            SetupMessage(MiningGovernance, Miner);
            SetupBlock(currentBlock);

            // Transfer rewards to mining pool
            var transferToParams = new object[] { MiningPool4, miningPoolReward };
            SetupCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToParams, TransferResult.Transferred(true));

            // Notify Mining Pool
            var notifyParams = new object[] {miningPoolReward};
            SetupCall(MiningPool4, 0ul, NotifyRewardAmountMethod, notifyParams, TransferResult.Transferred(null));
            
            // Get Balance of Mining Governance
            var getBalanceParams = new object[] { MiningGovernance };
            SetupCall(ODX, 0ul, nameof(IOpdexMinedToken.GetBalance), getBalanceParams, TransferResult.Transferred((UInt256)1));

            PersistentState.SetArray(nameof(IOpdexMiningGovernance.Nominations), nominations);
            PersistentState.SetUInt256(nameof(IOpdexMiningGovernance.MiningPoolReward), miningPoolReward);
            PersistentState.SetUInt32(nameof(IOpdexMiningGovernance.MiningPoolsFunded), 47);
            PersistentState.SetUInt64(nameof(IOpdexMiningGovernance.NominationPeriodEnd), nominationPeriodEnd);
            PersistentState.SetBool(nameof(IOpdexMiningGovernance.Notified), notified);
            PersistentState.SetAddress($"MiningPool:{Pool4}", MiningPool4);

            gov
                .Invoking(g => g.RewardMiningPool())
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_BALANCE");
        }
        
         [Fact]
        public void RewardMiningPool_Last_Throws_InvalidTransferTo()
        {
            UInt256 miningPoolReward = 100_000_000;
            const bool notified = true;
            const ulong nominationPeriodEnd = 100_000;
            const ulong currentBlock = 100_001;

            var gov = CreateNewOpdexMiningGovernance();
            var nominations = new[]
            {
                new Nomination {StakingPool = Address.Zero, Weight = UInt256.Zero},
                new Nomination {StakingPool = Address.Zero, Weight = UInt256.Zero},
                new Nomination {StakingPool = Address.Zero, Weight = UInt256.Zero},
                new Nomination {StakingPool = Pool4, Weight = 200_000_000}
            };

            SetupMessage(MiningGovernance, Miner);
            SetupBlock(currentBlock);

            var transferToParams = new object[] { MiningPool4, miningPoolReward };
            SetupCall(ODX, 0ul, nameof(IOpdexMinedToken.TransferTo), transferToParams, TransferResult.Failed());

            PersistentState.SetArray(nameof(IOpdexMiningGovernance.Nominations), nominations);
            PersistentState.SetUInt256(nameof(IOpdexMiningGovernance.MiningPoolReward), miningPoolReward);
            PersistentState.SetUInt32(nameof(IOpdexMiningGovernance.MiningPoolsFunded), 47);
            PersistentState.SetUInt64(nameof(IOpdexMiningGovernance.NominationPeriodEnd), nominationPeriodEnd);
            PersistentState.SetBool(nameof(IOpdexMiningGovernance.Notified), notified);
            PersistentState.SetAddress($"MiningPool:{Pool4}", MiningPool4);

            gov
                .Invoking(g => g.RewardMiningPool())
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_TRANSFER_TO");
        }

        #endregion

        [Fact]
        public void SerializeAddresses_Success()
        {
            var token1 = Serializer.ToAddress("P95Rx5Ts6Q5zsTYJYL49zJy75Le7x3neV2");
            var token2 = Serializer.ToAddress("PDArZge8uxydeNGcN21Fc8fQ9AmsVKQLeR");
            var token3 = Serializer.ToAddress("PFN3DovPUpgSJZivtQ8BDDsAHcZniUmshx");
            var token4 = Serializer.ToAddress("PVEYZzRJs9bEYw2VQG2o7AfCsUtX6cYGBv");
        
            _testOutputHelper.WriteLine(token1.ToString());
            
            var hexString = Serializer.Serialize(new[] {token1, token2, token3, token4}).ToHexString();
            
            _testOutputHelper.WriteLine(hexString);
        
            var addresses = Serializer.ToArray<Address>(hexString.HexToByteArray());

            foreach (var address in addresses)
            {
                _testOutputHelper.WriteLine(address.ToString());
            }
        }
    }
}