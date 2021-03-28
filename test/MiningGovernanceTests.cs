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
            gov.NominationPeriodEnd.Should().Be(41_062 + genesis);
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
        
        [Fact]
        public void NotifyDistribution_Success_Locked()
        {
            var gov = CreateNewOpdexMiningGovernance();

            SetupMessage(MiningGovernance, OPDX);
            
            PersistentState.SetBool(nameof(IMiningGovernance.Locked), true);
            
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

            SetupMessage(MiningGovernance, Miner1);
            
            gov
                .Invoking(g => g.NotifyDistribution(new byte[0]))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_SENDER");
        }
        
        [Fact]
        public void NominateLiquidityPool_NoNominations_Success()
        {
            var gov = CreateNewOpdexMiningGovernance();

            SetupMessage(MiningGovernance, OPDX);

            var miningPool1Params = new object[] {MiningGovernance, OPDX, Pool1};
            SetupCreate<MiningPool>(CreateResult.Succeeded(MiningPool1), 0ul, miningPool1Params);

            PersistentState.SetUInt64(nameof(IMiningGovernance.NominationPeriodEnd), 100_000);
            
            gov.NominateLiquidityPool(Pool1, 100_000_000);
            gov.Nominations.Length.Should().Be(1);
            
            VerifyCreate<MiningPool>(0ul, miningPool1Params, Times.Once);
            
            VerifyLog(new MiningPoolCreatedEvent
            {
                MiningPool = MiningPool1, 
                StakingPool = Pool1
            }, Times.Once);

            VerifyLog(new NominationEvent
            {
                StakingPool = Pool1,
                Weight = 100_000_000,
                MiningPool = MiningPool1
            }, Times.Once);
        }

        [Fact]
        public void NominateLiquidityPool_LessThanMaxNominations_Success()
        {
            var nominations = new[]
            {
                new Nomination {StakingPool = Pool1, Weight = 50_000_000},
                new Nomination {StakingPool = Pool2, Weight = 150_000_000},
                new Nomination {StakingPool = Pool3, Weight = 90_000_000}
            };
            
            var gov = CreateNewOpdexMiningGovernance();

            SetupMessage(MiningGovernance, OPDX);

            var miningPool4Params = new object[] {MiningGovernance, OPDX, Pool4};
            SetupCreate<MiningPool>(CreateResult.Succeeded(MiningPool4), 0ul, miningPool4Params);

            PersistentState.SetArray(nameof(IMiningGovernance.Nominations), nominations);
            PersistentState.SetUInt64(nameof(IMiningGovernance.NominationPeriodEnd), 100_000);
            
            gov.NominateLiquidityPool(Pool4, 100_000_000);
            gov.Nominations.Length.Should().Be(4);
            
            VerifyCreate<MiningPool>(0ul, miningPool4Params, Times.Once);
            
            VerifyLog(new MiningPoolCreatedEvent
            {
                MiningPool = MiningPool4, 
                StakingPool = Pool4
            }, Times.Once);

            VerifyLog(new NominationEvent
            {
                StakingPool = Pool4,
                Weight = 100_000_000,
                MiningPool = MiningPool4
            }, Times.Once);
        }
        
        [Fact]
        public void NominateLiquidityPool_ExistingNominationUpdate_Success()
        {
            var nominations = new[]
            {
                new Nomination {StakingPool = Pool1, Weight = 50_000_000},
                new Nomination {StakingPool = Pool2, Weight = 150_000_000},
                new Nomination {StakingPool = Pool3, Weight = 90_000_000}
            };
            
            var gov = CreateNewOpdexMiningGovernance();

            SetupMessage(MiningGovernance, OPDX);

            PersistentState.SetArray(nameof(IMiningGovernance.Nominations), nominations);
            PersistentState.SetUInt64(nameof(IMiningGovernance.NominationPeriodEnd), 100_000);
            PersistentState.SetAddress($"MiningPool:{Pool3}", MiningPool3);
            
            gov.NominateLiquidityPool(Pool3, 100_000_000);
            gov.Nominations.Length.Should().Be(3);
            gov.Nominations[2].Weight.Should().Be((UInt256)100_000_000);

            VerifyLog(new NominationEvent
            {
                StakingPool = Pool3,
                Weight = 100_000_000,
                MiningPool = MiningPool3
            }, Times.Once);
        }
        
        [Fact]
        public void NominateLiquidityPool_Does_QualifyAgainstExistingNominations_Success()
        {
            var nominations = new[]
            {
                new Nomination {StakingPool = Pool1, Weight = 300_000_000},
                new Nomination {StakingPool = Pool2, Weight = 50_000_000},
                new Nomination {StakingPool = Pool3, Weight = 190_000_000},
                new Nomination {StakingPool = Pool4, Weight = 200_000_000}
            };
            
            var gov = CreateNewOpdexMiningGovernance();

            SetupMessage(MiningGovernance, OPDX);

            var miningPool5Params = new object[] {MiningGovernance, OPDX, Pool5};
            SetupCreate<MiningPool>(CreateResult.Succeeded(MiningPool5), 0ul, miningPool5Params);

            PersistentState.SetArray(nameof(IMiningGovernance.Nominations), nominations);
            PersistentState.SetUInt64(nameof(IMiningGovernance.NominationPeriodEnd), 100_000);
            
            gov.NominateLiquidityPool(Pool5, 100_000_000);
            gov.Nominations.Length.Should().Be(4);
            var newNomination = gov.Nominations[1];
            newNomination.StakingPool.Should().Be(Pool5);
            newNomination.Weight.Should().Be((UInt256)100_000_000);
            
            VerifyCreate<MiningPool>(0ul, miningPool5Params, Times.Once);
            
            VerifyLog(new MiningPoolCreatedEvent
            {
                MiningPool = MiningPool5, 
                StakingPool = Pool5
            }, Times.Once);

            VerifyLog(new NominationEvent
            {
                StakingPool = Pool5,
                Weight = 100_000_000,
                MiningPool = MiningPool5
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

            SetupMessage(MiningGovernance, OPDX);

            PersistentState.SetArray(nameof(IMiningGovernance.Nominations), nominations);
            PersistentState.SetUInt64(nameof(IMiningGovernance.NominationPeriodEnd), 100_000);
            
            gov.NominateLiquidityPool(Pool5, 150_000_000);

            for (var i = 0; i < nominations.Length; i++)
            {
                gov.Nominations[i].StakingPool.Should().Be(nominations[i].StakingPool);
                gov.Nominations[i].Weight.Should().Be(nominations[i].Weight);
            }

            VerifyLog(new NominationEvent
            {
                StakingPool = It.IsAny<Address>(),
                Weight = It.IsAny<UInt256>(),
                MiningPool = It.IsAny<Address>()
            }, Times.Never);
        }

        [Fact]
        public void NominateLiquidityPool_Throws_InvalidSender()
        {
            var nominationWeight = new UInt256("100000000000000000000");
            
            var gov = CreateNewOpdexMiningGovernance();

            SetupMessage(MiningGovernance, Miner1);
            
            gov
                .Invoking(g => g.NominateLiquidityPool(Pool5, nominationWeight))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_SENDER");
        }
        
        #region Notify Mining Pool

        [Fact]
        public void NotifyMiningPool_NotLast_Success()
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

            SetupMessage(MiningGovernance, Miner1);
            SetupBlock(ulong.MaxValue);

            var transferToParams = new object[] { MiningPool1, miningPoolReward };
            SetupCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToParams, TransferResult.Transferred(true));

            SetupCall(MiningPool1, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, TransferResult.Transferred(null));
            
            PersistentState.SetArray(nameof(IMiningGovernance.Nominations), nominations);
            PersistentState.SetAddress($"MiningPool:{Pool1}", MiningPool1);
            PersistentState.SetUInt256(nameof(IMiningGovernance.MiningPoolReward), miningPoolReward);

            gov.NotifyMiningPool();
            gov.MiningPoolsFunded.Should().Be(1);
            gov.Nominations[0].StakingPool.Should().Be(Address.Zero);
            gov.Nominations[0].Weight.Should().Be(UInt256.Zero);

            VerifyCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToParams, Times.Once);
            VerifyCall(MiningPool1, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, Times.Once);

            VerifyLog(new MiningPoolRewardedEvent
            {
                StakingPool = Pool1,
                MiningPool = MiningPool1,
                Amount = miningPoolReward
            }, Times.Once);
        }
        
        [Fact]
        public void NotifyMiningPool_Last_Success()
        {
            UInt256 miningPoolReward = 100_000_000;
            const uint miningPoolsPerYear = 48;
            const bool distributed = true;
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

            SetupMessage(MiningGovernance, Miner1);
            SetupBlock(currentBlock);

            // Transfer rewards to mining pool
            var transferToParams = new object[] { MiningPool4, miningPoolReward };
            SetupCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToParams, TransferResult.Transferred(true));

            // Notify Mining Pool
            SetupCall(MiningPool4, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, TransferResult.Transferred(null));
            
            // Get Balance of Mining Governance
            var getBalanceParams = new object[] { MiningGovernance };
            SetupCall(OPDX, 0ul, nameof(IOpdexToken.GetBalance), getBalanceParams, TransferResult.Transferred(miningPoolReward * 2 * miningPoolsPerYear));

            PersistentState.SetArray(nameof(IMiningGovernance.Nominations), nominations);
            PersistentState.SetAddress($"MiningPool:{Pool4}", MiningPool4);
            PersistentState.SetUInt256(nameof(IMiningGovernance.MiningPoolReward), miningPoolReward);
            PersistentState.SetUInt32(nameof(IMiningGovernance.MiningPoolsFunded), 47);
            PersistentState.SetUInt64(nameof(IMiningGovernance.NominationPeriodEnd), nominationPeriodEnd);
            PersistentState.SetBool(nameof(IMiningGovernance.Distributed), distributed);

            gov.NotifyMiningPool();
            gov.MiningPoolsFunded.Should().Be(0);
            gov.Nominations.Length.Should().Be(0);
            gov.NominationPeriodEnd.Should().Be(currentBlock + 164250); // one months blocks
            gov.Distributed.Should().BeFalse();
            gov.MiningPoolReward.Should().Be(miningPoolReward * 2);

            VerifyCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToParams, Times.Once);
            VerifyCall(MiningPool4, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, Times.Once);
            VerifyCall(OPDX, 0ul, nameof(IOpdexToken.GetBalance), getBalanceParams, Times.Once);

            VerifyLog(new MiningPoolRewardedEvent
            {
                StakingPool = Pool4,
                MiningPool = MiningPool4,
                Amount = miningPoolReward
            }, Times.Once);
        }

        [Fact]
        public void NotifyMiningPool_Last_Throws_GovernanceAwaitingDistribution()
        {
            UInt256 miningPoolReward = 100_000_000;
            const uint miningPoolsPerYear = 48;
            const bool distributed = false;
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

            SetupMessage(MiningGovernance, Miner1);
            SetupBlock(currentBlock);

            // Transfer rewards to mining pool
            var transferToParams = new object[] { MiningPool4, miningPoolReward };
            SetupCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToParams, TransferResult.Transferred(true));

            // Notify Mining Pool
            SetupCall(MiningPool4, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, TransferResult.Transferred(null));
            
            // Get Balance of Mining Governance
            var getBalanceParams = new object[] { MiningGovernance };
            SetupCall(OPDX, 0ul, nameof(IOpdexToken.GetBalance), getBalanceParams, TransferResult.Transferred(miningPoolReward * 2 * miningPoolsPerYear));

            PersistentState.SetArray(nameof(IMiningGovernance.Nominations), nominations);
            PersistentState.SetAddress($"MiningPool:{Pool4}", MiningPool4);
            PersistentState.SetUInt256(nameof(IMiningGovernance.MiningPoolReward), miningPoolReward);
            PersistentState.SetUInt32(nameof(IMiningGovernance.MiningPoolsFunded), 47);
            PersistentState.SetUInt64(nameof(IMiningGovernance.NominationPeriodEnd), nominationPeriodEnd);
            PersistentState.SetBool(nameof(IMiningGovernance.Distributed), distributed);

            gov
                .Invoking(g => g.NotifyMiningPool())
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: TOKEN_DISTRIBUTION_REQUIRED");
        }

        #endregion
        
        #region Notify Mining Pools

        [Fact]
        public void NotifyMiningPools_All_Success()
        {
            UInt256 miningPoolReward = 100_000_000;
            const ulong currentBlock = 100_001;
            
            var gov = CreateNewOpdexMiningGovernance();
            var nominations = new[]
            {
                new Nomination {StakingPool = Pool1, Weight = 300_000_000},
                new Nomination {StakingPool = Pool2, Weight = 150_000_000},
                new Nomination {StakingPool = Pool3, Weight = 190_000_000},
                new Nomination {StakingPool = Pool4, Weight = 200_000_000}
            };

            SetupMessage(MiningGovernance, Miner1);
            SetupBlock(currentBlock);

            var transferToPool1Params = new object[] { MiningPool1, miningPoolReward };
            SetupCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToPool1Params, TransferResult.Transferred(true));
            
            var transferToPool2Params = new object[] { MiningPool2, miningPoolReward };
            SetupCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToPool2Params, TransferResult.Transferred(true));
            
            var transferToPool3Params = new object[] { MiningPool3, miningPoolReward };
            SetupCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToPool3Params, TransferResult.Transferred(true));
            
            var transferToPool4Params = new object[] { MiningPool4, miningPoolReward };
            SetupCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToPool4Params, TransferResult.Transferred(true));

            SetupCall(MiningPool1, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, TransferResult.Transferred(null));
            SetupCall(MiningPool2, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, TransferResult.Transferred(null));
            SetupCall(MiningPool3, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, TransferResult.Transferred(null));
            SetupCall(MiningPool4, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, TransferResult.Transferred(null));
            
            PersistentState.SetArray(nameof(IMiningGovernance.Nominations), nominations);
            PersistentState.SetUInt256(nameof(IMiningGovernance.MiningPoolReward), miningPoolReward);
            PersistentState.SetAddress($"MiningPool:{Pool1}", MiningPool1);
            PersistentState.SetAddress($"MiningPool:{Pool2}", MiningPool2);
            PersistentState.SetAddress($"MiningPool:{Pool3}", MiningPool3);
            PersistentState.SetAddress($"MiningPool:{Pool4}", MiningPool4);

            gov.NotifyMiningPools();
            gov.MiningPoolsFunded.Should().Be(4);
            gov.Nominations.Length.Should().Be(0);
            gov.NominationPeriodEnd.Should().Be(currentBlock + 164250);

            // Todo: Resolve TestBase helper issue. This test is passing
            // VerifyCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToPool1Params, Times.Once);
            // VerifyCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToPool2Params, Times.Once);
            // VerifyCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToPool3Params, Times.Once);
            // VerifyCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToPool4Params, Times.Once);
            
            VerifyCall(MiningPool1, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, Times.Once);
            VerifyCall(MiningPool2, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, Times.Once);
            VerifyCall(MiningPool3, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, Times.Once);
            VerifyCall(MiningPool4, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, Times.Once);

            VerifyLog(new MiningPoolRewardedEvent { StakingPool = Pool1, MiningPool = MiningPool1, Amount = miningPoolReward }, Times.Once);
            VerifyLog(new MiningPoolRewardedEvent { StakingPool = Pool2, MiningPool = MiningPool2, Amount = miningPoolReward }, Times.Once);
            VerifyLog(new MiningPoolRewardedEvent { StakingPool = Pool3, MiningPool = MiningPool3, Amount = miningPoolReward }, Times.Once);
            VerifyLog(new MiningPoolRewardedEvent { StakingPool = Pool4, MiningPool = MiningPool4, Amount = miningPoolReward }, Times.Once);
        }
        
        [Fact]
        public void NotifyMiningPools_Remaining_Success()
        {
            UInt256 miningPoolReward = 100_000_000;
            const ulong currentBlock = 100_001;
            
            var gov = CreateNewOpdexMiningGovernance();
            var nominations = new[]
            {
                new Nomination {StakingPool = Address.Zero, Weight = UInt256.Zero},
                new Nomination {StakingPool = Pool2, Weight = 150_000_000},
                new Nomination {StakingPool = Pool3, Weight = 190_000_000},
                new Nomination {StakingPool = Pool4, Weight = 200_000_000}
            };

            SetupMessage(MiningGovernance, Miner1);
            SetupBlock(currentBlock);

            var transferToPool2Params = new object[] { MiningPool2, miningPoolReward };
            SetupCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToPool2Params, TransferResult.Transferred(true));
            
            var transferToPool3Params = new object[] { MiningPool3, miningPoolReward };
            SetupCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToPool3Params, TransferResult.Transferred(true));
            
            var transferToPool4Params = new object[] { MiningPool4, miningPoolReward };
            SetupCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToPool4Params, TransferResult.Transferred(true));

            SetupCall(MiningPool2, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, TransferResult.Transferred(null));
            SetupCall(MiningPool3, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, TransferResult.Transferred(null));
            SetupCall(MiningPool4, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, TransferResult.Transferred(null));
            
            PersistentState.SetArray(nameof(IMiningGovernance.Nominations), nominations);
            PersistentState.SetUInt256(nameof(IMiningGovernance.MiningPoolReward), miningPoolReward);
            PersistentState.SetUInt32(nameof(IMiningGovernance.MiningPoolsFunded), 1);
            PersistentState.SetAddress($"MiningPool:{Pool1}", MiningPool1);
            PersistentState.SetAddress($"MiningPool:{Pool2}", MiningPool2);
            PersistentState.SetAddress($"MiningPool:{Pool3}", MiningPool3);
            PersistentState.SetAddress($"MiningPool:{Pool4}", MiningPool4);

            gov.NotifyMiningPools();
            gov.MiningPoolsFunded.Should().Be(4);
            gov.Nominations.Length.Should().Be(0);
            gov.NominationPeriodEnd.Should().Be(currentBlock + 164250);

            // Todo: Resolve TestBase helper issue. This test is passing
            // VerifyCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToPool1Params, Times.Once);
            // VerifyCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToPool2Params, Times.Once);
            // VerifyCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToPool3Params, Times.Once);
            // VerifyCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToPool4Params, Times.Once);
            
            VerifyCall(MiningPool1, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, Times.Never);
            VerifyCall(MiningPool2, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, Times.Once);
            VerifyCall(MiningPool3, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, Times.Once);
            VerifyCall(MiningPool4, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, Times.Once);
            
            VerifyLog(new MiningPoolRewardedEvent { StakingPool = Pool2, MiningPool = MiningPool2, Amount = miningPoolReward }, Times.Once);
            VerifyLog(new MiningPoolRewardedEvent { StakingPool = Pool3, MiningPool = MiningPool3, Amount = miningPoolReward }, Times.Once);
            VerifyLog(new MiningPoolRewardedEvent { StakingPool = Pool4, MiningPool = MiningPool4, Amount = miningPoolReward }, Times.Once);
        }
        
        [Fact]
        public void NotifyMiningPools_All_ResetYearly_Success()
        {
            UInt256 miningPoolReward = 100_000_000;
            const ulong currentBlock = 100_001;
            const bool distributed = true;
            const uint miningPoolsPerYear = 48;
            
            var gov = CreateNewOpdexMiningGovernance();
            var nominations = new[]
            {
                new Nomination {StakingPool = Pool1, Weight = 300_000_000},
                new Nomination {StakingPool = Pool2, Weight = 150_000_000},
                new Nomination {StakingPool = Pool3, Weight = 190_000_000},
                new Nomination {StakingPool = Pool4, Weight = 200_000_000}
            };

            SetupMessage(MiningGovernance, Miner1);
            SetupBlock(currentBlock);

            var transferToPool1Params = new object[] { MiningPool1, miningPoolReward };
            SetupCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToPool1Params, TransferResult.Transferred(true));
            
            var transferToPool2Params = new object[] { MiningPool2, miningPoolReward };
            SetupCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToPool2Params, TransferResult.Transferred(true));
            
            var transferToPool3Params = new object[] { MiningPool3, miningPoolReward };
            SetupCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToPool3Params, TransferResult.Transferred(true));
            
            var transferToPool4Params = new object[] { MiningPool4, miningPoolReward };
            SetupCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToPool4Params, TransferResult.Transferred(true));

            SetupCall(MiningPool1, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, TransferResult.Transferred(null));
            SetupCall(MiningPool2, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, TransferResult.Transferred(null));
            SetupCall(MiningPool3, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, TransferResult.Transferred(null));
            SetupCall(MiningPool4, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, TransferResult.Transferred(null));
            
            // Get Balance of Mining Governance
            var getBalanceParams = new object[] { MiningGovernance };
            SetupCall(OPDX, 0ul, nameof(IOpdexToken.GetBalance), getBalanceParams, TransferResult.Transferred(miningPoolReward * 2 * miningPoolsPerYear));
            
            PersistentState.SetArray(nameof(IMiningGovernance.Nominations), nominations);
            PersistentState.SetUInt256(nameof(IMiningGovernance.MiningPoolReward), miningPoolReward);
            PersistentState.SetBool(nameof(IMiningGovernance.Distributed), distributed);
            PersistentState.SetUInt32(nameof(IMiningGovernance.MiningPoolsFunded), 44);
            PersistentState.SetAddress($"MiningPool:{Pool1}", MiningPool1);
            PersistentState.SetAddress($"MiningPool:{Pool2}", MiningPool2);
            PersistentState.SetAddress($"MiningPool:{Pool3}", MiningPool3);
            PersistentState.SetAddress($"MiningPool:{Pool4}", MiningPool4);

            gov.NotifyMiningPools();
            gov.MiningPoolsFunded.Should().Be(0);
            gov.Nominations.Length.Should().Be(0);
            gov.NominationPeriodEnd.Should().Be(currentBlock + 164250);
            gov.Distributed.Should().BeFalse();
            gov.MiningPoolReward.Should().Be(miningPoolReward * 2);

            // Todo: Resolve TestBase helper issue. This test is passing
            // VerifyCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToPool1Params, Times.Once);
            // VerifyCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToPool2Params, Times.Once);
            // VerifyCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToPool3Params, Times.Once);
            // VerifyCall(OPDX, 0ul, nameof(IOpdexToken.TransferTo), transferToPool4Params, Times.Once);
            
            VerifyCall(MiningPool1, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, Times.Once);
            VerifyCall(MiningPool2, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, Times.Once);
            VerifyCall(MiningPool3, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, Times.Once);
            VerifyCall(MiningPool4, 0ul, nameof(IMiningPool.NotifyRewardAmount), null, Times.Once);
            
            VerifyCall(OPDX, 0ul, nameof(IOpdexToken.GetBalance), getBalanceParams, Times.Once);

            VerifyLog(new MiningPoolRewardedEvent { StakingPool = Pool1, MiningPool = MiningPool1, Amount = miningPoolReward }, Times.Once);
            VerifyLog(new MiningPoolRewardedEvent { StakingPool = Pool2, MiningPool = MiningPool2, Amount = miningPoolReward }, Times.Once);
            VerifyLog(new MiningPoolRewardedEvent { StakingPool = Pool3, MiningPool = MiningPool3, Amount = miningPoolReward }, Times.Once);
            VerifyLog(new MiningPoolRewardedEvent { StakingPool = Pool4, MiningPool = MiningPool4, Amount = miningPoolReward }, Times.Once);
        }
        
        #endregion
    }
}