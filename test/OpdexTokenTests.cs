using System;
using FluentAssertions;
using Moq;
using OpdexTokenTests.Base;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core;
using Xunit;

namespace OpdexTokenTests
{
    public class OpdexTokenTests : TestBase
    {
        private readonly UInt256[] DefaultOwnerSchedule = { 120_000_000, 90_000_000, 60_000_000, 30_000_000, 7_500_000 };
        private readonly UInt256[] DefaultMiningSchedule = { 280_000_000, 210_000_000, 140_000_000, 70_000_000, 17_500_000 };
        
        [Fact]
        public void CreateContract_Success()
        {
            var ownerSchedule = Serializer.Serialize(DefaultOwnerSchedule);
            var miningSchedule = Serializer.Serialize(DefaultMiningSchedule);
            
            var token = CreateNewOpdexToken(ownerSchedule, miningSchedule);

            token.Owner.Should().Be(Owner);
            token.Name.Should().Be("Opdex");
            token.Symbol.Should().Be("OPDX");
            token.Decimals.Should().Be(18);
            token.Genesis.Should().Be(10ul);
            token.TotalSupply.Should().Be(UInt256.Zero);
            token.GetBalance(Owner).Should().Be(UInt256.Zero);
            token.GetBalance(MiningGovernance).Should().Be(UInt256.Zero);
            token.OwnerSchedule.Should().Equal(DefaultOwnerSchedule);
            token.MiningSchedule.Should().Equal(DefaultMiningSchedule);
        }

        [Fact]
        public void CreateContract_Throws_InvalidScheduleLengths()
        {
            var ownerSchedule = Serializer.Serialize(new UInt256[] { 12345 });
            var miningSchedule = Serializer.Serialize(new UInt256[] { 9876 });

            var threw = false;
            
            try
            {
                CreateNewOpdexToken(ownerSchedule, miningSchedule);
            }
            catch (SmartContractAssertException)
            {
                threw = true;
            }

            threw.Should().BeTrue();
        }
        
        [Fact]
        public void CreateContract_Throws_DifferentScheduleLengths()
        {
            var ownerSchedule = Serializer.Serialize(new UInt256[] { 125 });
            var miningSchedule = Serializer.Serialize(new UInt256[0]);

            var threw = false;
            
            try
            {
                CreateNewOpdexToken(ownerSchedule, miningSchedule);
            }
            catch (SmartContractAssertException)
            {
                threw = true;
            }

            threw.Should().BeTrue();
        }

        [Fact]
        public void SetOwner_Success()
        {
            var ownerSchedule = Serializer.Serialize(DefaultOwnerSchedule);
            var miningSchedule = Serializer.Serialize(DefaultMiningSchedule);
            
            var token = CreateNewOpdexToken(ownerSchedule, miningSchedule);
            
            SetupMessage(OPDX, Owner);
            
            token.SetOwner(MiningGovernance);

            token.Owner.Should().Be(MiningGovernance);
        }
        
        [Fact]
        public void SetOwner_Throws_Unauthorized()
        {
            var ownerSchedule = Serializer.Serialize(DefaultOwnerSchedule);
            var miningSchedule = Serializer.Serialize(DefaultMiningSchedule);
            
            var token = CreateNewOpdexToken(ownerSchedule, miningSchedule);
            
            SetupMessage(OPDX, Miner1);

            token.Invoking(t => t.SetOwner(MiningGovernance))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }

        [Fact]
        public void Distribute_YearOne_Success()
        {
            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultOwnerSchedule), Serializer.Serialize(DefaultMiningSchedule));

            var stakingTokens = Serializer.Serialize(new[] { Miner1, Pool1, OPDX, Owner }); // Any 4 address, not important for this test

            var createParams = new object[] { OPDX };
            SetupCreate<MiningGovernance>(CreateResult.Succeeded(MiningGovernance), 0ul, createParams);

            var callParams = new object[] { stakingTokens };
            SetupCall(MiningGovernance, 0ul, nameof(IMiningGovernance.NotifyDistribution), callParams, TransferResult.Transferred(null));
            
            token.Distribute(stakingTokens);
            
            token.GetBalance(Owner).Should().Be(DefaultOwnerSchedule[0]);
            token.GetBalance(MiningGovernance).Should().Be(DefaultMiningSchedule[0]);
            token.YearIndex.Should().Be(1);
            token.TotalSupply.Should().Be((UInt256)400_000_000);

            VerifyCreate<MiningGovernance>(0ul, createParams, Times.Once);
            VerifyCall(MiningGovernance, 0ul, nameof(IMiningGovernance.NotifyDistribution), callParams, Times.Once);
            VerifyLog(new DistributionEvent
            {
                OwnerAddress = Owner,
                MiningAddress = MiningGovernance,
                OwnerAmount = DefaultOwnerSchedule[0],
                MiningAmount = DefaultMiningSchedule[0],
                YearIndex = 0
            }, Times.Once);
        }

        [Theory]
        [InlineData(1, 120_000_000, 280_000_000, 400_000_000, 210_000_000, 490_000_000, 700_000_000)]
        [InlineData(2, 210_000_000, 490_000_000, 700_000_000, 270_000_000, 630_000_000, 900_000_000)]
        [InlineData(3, 270_000_000, 630_000_000, 900_000_000, 300_000_000, 700_000_000, 1_000_000_000)]
        [InlineData(4, 300_000_000, 700_000_000, 1_000_000_000, 307_500_000, 717_500_000, 1_025_000_000)]
        [InlineData(5, 307_500_000, 717_500_000, 1_025_000_000, 315_000_000, 735_000_000, 1_050_000_000)]
        [InlineData(6, 315_000_000, 735_000_000, 1_050_000_000, 322_500_000, 752_500_000, 1_075_000_000)]
        public void Distribute_SubsequentYears_Success(uint yearIndex, UInt256 currentOwnerBalance, UInt256 currentMiningBalance, UInt256 currentTotalSupply,
            UInt256 expectedOwnerBalance, UInt256 expectedMiningBalance, UInt256 expectedTotalSupply)
        {
            const ulong genesis = 100;
            const ulong blocksPerYear = 1_971_000;
            
            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultOwnerSchedule), Serializer.Serialize(DefaultMiningSchedule), genesis);
            
            PersistentState.SetAddress(nameof(MiningGovernance), MiningGovernance);
            PersistentState.SetUInt256($"Balance:{Owner}", currentOwnerBalance);
            PersistentState.SetUInt256($"Balance:{MiningGovernance}", currentMiningBalance);
            PersistentState.SetUInt32(nameof(OpdexToken.YearIndex), yearIndex);
            PersistentState.SetUInt256(nameof(OpdexToken.TotalSupply), currentTotalSupply);

            var block = (blocksPerYear * yearIndex) + genesis;
            SetupBlock(block);

            var notifyParams = new object[] {new byte[0]};
            SetupCall(MiningGovernance, 0ul, nameof(IMiningGovernance.NotifyDistribution), notifyParams, TransferResult.Transferred(null));
            
            token.Distribute(new byte[0]);
            
            token.GetBalance(Owner).Should().Be(expectedOwnerBalance);
            token.GetBalance(MiningGovernance).Should().Be(expectedMiningBalance);
            token.YearIndex.Should().Be(yearIndex + 1);
            token.TotalSupply.Should().Be(expectedTotalSupply);

            var scheduleIndex = yearIndex > (uint) DefaultOwnerSchedule.Length - 2
                ? (uint) DefaultOwnerSchedule.Length - 1
                : yearIndex;
            
            VerifyCall(MiningGovernance, 0ul, nameof(IMiningGovernance.NotifyDistribution), notifyParams, Times.Once);
            
            VerifyLog(new DistributionEvent
            {
                OwnerAddress = Owner,
                MiningAddress = MiningGovernance,
                OwnerAmount = DefaultOwnerSchedule[scheduleIndex],
                MiningAmount = DefaultMiningSchedule[scheduleIndex],
                YearIndex = yearIndex
            }, Times.Once);
        }
        
        [Fact]
        public void Distribute_Throws_TooEarly()
        {
            const ulong genesis = 100;
            const ulong blocksPerYear = 1_971_000;
            const uint yearIndex = 1;
            
            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultOwnerSchedule), Serializer.Serialize(DefaultMiningSchedule), genesis);
            
            PersistentState.SetAddress(nameof(MiningGovernance), MiningGovernance);
            PersistentState.SetUInt32(nameof(OpdexToken.YearIndex), yearIndex);

            const ulong earlyBlock = ((blocksPerYear + genesis) * yearIndex) - 100;
            
            SetupBlock(earlyBlock);

            token.Invoking(t => t.Distribute(new byte[0]))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: DISTRIBUTION_NOT_READY");
        }

        [Fact]
        public void Serialize_Distribution_Schedules()
        {
            Console.WriteLine(Serializer.Serialize(DefaultOwnerSchedule).ToHexString());
            Console.WriteLine(Serializer.Serialize(DefaultMiningSchedule).ToHexString());
        }
    }
}