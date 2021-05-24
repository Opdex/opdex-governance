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
    public class OpdexTokenTests : TestBase
    {
        private readonly ITestOutputHelper _testOutputHelper;

        // 100M, 75M, 50M, 25M, 5M
        private readonly UInt256[] DefaultVaultSchedule = { 10_000_000_000_000_000, 75_000_00000_000_000, 50_000_00000_000_000, 25_000_00000_000_000, 0 };
        
        // 300M, 225M, 150M, 75M, 20M
        private readonly UInt256[] DefaultMiningSchedule = { 300_000_00000_000_000, 225_000_00000_000_000, 150_000_00000_000_000, 75_000_00000_000_000, 25_000_00000_000_000 };

        public OpdexTokenTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void CreateContract_Success()
        {
            var ownerSchedule = Serializer.Serialize(DefaultVaultSchedule);
            var miningSchedule = Serializer.Serialize(DefaultMiningSchedule);
            
            var token = CreateNewOpdexToken(ownerSchedule, miningSchedule);

            token.Creator.Should().Be(Owner);
            token.Name.Should().Be("Opdex");
            token.Symbol.Should().Be("ODX");
            token.Decimals.Should().Be(8);
            token.Genesis.Should().Be(0ul);
            token.TotalSupply.Should().Be(UInt256.Zero);
            token.MiningGovernance.Should().Be(MiningGovernance);
            token.GetBalance(Owner).Should().Be(UInt256.Zero);
            token.GetBalance(MiningGovernance).Should().Be(UInt256.Zero);
            token.VaultSchedule.Should().Equal(DefaultVaultSchedule);
            token.MiningSchedule.Should().Equal(DefaultMiningSchedule);
            token.PeriodDuration.Should().Be(BlocksPerYear);
            token.Vault.Should().Be(Vault);
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
        public void Distribute_InitialPeriod_Success()
        {
            var block = 100ul;
            
            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule));

            var stakingTokens = Serializer.Serialize(new[] { Miner1, Pool1, ODX, Owner }); // Any 4 address, not important for this test

            var createParams = new object[] { ODX, BlocksPerMonth };
            SetupCreate<OpdexMiningGovernance>(CreateResult.Succeeded(MiningGovernance), 0ul, createParams);

            var governanceCallParams = new object[] { stakingTokens };
            SetupCall(MiningGovernance, 0ul, nameof(IOpdexMiningGovernance.NotifyDistribution), governanceCallParams, TransferResult.Transferred(null));

            var vaultCallParams = new object[] {DefaultVaultSchedule[0]};
            SetupCall(Vault, 0ul, nameof(IOpdexVault.NotifyDistribution), vaultCallParams, TransferResult.Transferred(null));
            
            SetupBlock(block);
            token.Distribute(stakingTokens);
            
            token.GetBalance(Vault).Should().Be(DefaultVaultSchedule[0]);
            token.GetBalance(MiningGovernance).Should().Be(DefaultMiningSchedule[0]);
            token.PeriodIndex.Should().Be(1);
            token.TotalSupply.Should().Be((UInt256)40_000_000_000_000_000);
            token.Genesis.Should().Be(block);

            VerifyCreate<OpdexMiningGovernance>(0ul, createParams, Times.Once);
            
            VerifyCall(MiningGovernance, 0ul, nameof(IOpdexMiningGovernance.NotifyDistribution), governanceCallParams, Times.Once);
            VerifyCall(Vault, 0ul, nameof(IOpdexVault.NotifyDistribution), vaultCallParams, Times.Once);
            
            VerifyLog(new DistributionLog
            {
                VaultAmount = DefaultVaultSchedule[0],
                MiningAmount = DefaultMiningSchedule[0],
                PeriodIndex = 0
            }, Times.Once);
        }

        [Theory]
        [InlineData(1, 10_000_000_000_000_000, 30_000_000_000_000_000, 40_000_000_000_000_000, 17_500_000_000_000_000, 52_500_000_000_000_000, 70_000_000_000_000_000)]
        [InlineData(2, 17_500_000_000_000_000, 52_500_000_000_000_000, 70_000_000_000_000_000, 22_500_000_000_000_000, 67_500_000_000_000_000, 90_000_000_000_000_000)]
        [InlineData(3, 22_500_000_000_000_000, 67_500_000_000_000_000, 90_000_000_000_000_000, 25_000_000_000_000_000, 75_000_000_000_000_000, 1_00_000_000_000_000_000)]
        [InlineData(4, 25_000_000_000_000_000, 75_000_000_000_000_000, 100_000_000_000_000_000, 25_000_000_000_000_000, 77_500_000_000_000_000, 102_500_000_000_000_000)]
        [InlineData(5, 25_000_000_000_000_000, 77_500_000_000_000_000, 102_500_000_000_000_000, 25_000_000_000_000_000, 80_000_000_000_000_000, 1_05_000_000_000_000_000)]
        [InlineData(6, 25_000_000_000_000_000, 80_000_000_000_000_000, 1_05_000_000_000_000_000, 25_000_000_000_000_000, 82_500_000_000_000_000, 107_500_000_000_000_000)]
        public void Distribute_SubsequentYears_Success(uint periodIndex, UInt256 currentVaultBalance, UInt256 currentMiningBalance, UInt256 currentTotalSupply,
            UInt256 expectedVaultBalance, UInt256 expectedMiningBalance, UInt256 expectedTotalSupply)
        {
            const ulong genesis = 100;
            const ulong periodDuration = 1000;
            
            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule), genesis);
            
            PersistentState.SetUInt256($"Balance:{Vault}", currentVaultBalance);
            PersistentState.SetUInt256($"Balance:{MiningGovernance}", currentMiningBalance);
            PersistentState.SetUInt32(nameof(IOpdexMinedToken.PeriodIndex), periodIndex);
            PersistentState.SetUInt64(nameof(IOpdexMinedToken.PeriodDuration), periodDuration);
            PersistentState.SetUInt256(nameof(IOpdexMinedToken.TotalSupply), currentTotalSupply);
            PersistentState.SetUInt64(nameof(IOpdexMinedToken.Genesis), genesis);

            var block = (BlocksPerYear * periodIndex) + genesis;
            SetupBlock(block);

            var notifyDistributionParameters = new object[] {new byte[0]};
            SetupCall(MiningGovernance, 0, nameof(IOpdexMiningGovernance.NotifyDistribution), notifyDistributionParameters, TransferResult.Transferred(null));
            
            var notifyVaultParams = new object[] {expectedVaultBalance - currentVaultBalance};
            SetupCall(Vault, 0, nameof(IOpdexVault.NotifyDistribution), notifyVaultParams, TransferResult.Transferred(null));
            
            token.Distribute(new byte[0]);
            
            token.GetBalance(Vault).Should().Be(expectedVaultBalance);
            token.GetBalance(MiningGovernance).Should().Be(expectedMiningBalance);
            token.PeriodIndex.Should().Be(periodIndex + 1);
            token.TotalSupply.Should().Be(expectedTotalSupply);
            token.Genesis.Should().Be(genesis);

            var scheduleIndex = periodIndex > (uint) DefaultVaultSchedule.Length - 2
                ? (uint)DefaultVaultSchedule.Length - 1
                : periodIndex;
            
            VerifyCall(MiningGovernance, 0, nameof(IOpdexMiningGovernance.NotifyDistribution), notifyDistributionParameters, Times.Once);

            if (currentVaultBalance < expectedVaultBalance)
            {
                VerifyCall(Vault, 0, nameof(IOpdexVault.NotifyDistribution), notifyVaultParams, Times.Once);
            }
            
            VerifyLog(new DistributionLog
            {
                VaultAmount = DefaultVaultSchedule[scheduleIndex],
                MiningAmount = DefaultMiningSchedule[scheduleIndex],
                PeriodIndex = periodIndex
            }, Times.Once);
        }
        
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        public void Distribute_Throws_TooEarly(uint periodIndex)
        {
            const ulong genesis = 100;
            
            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule), genesis);
            
            PersistentState.SetAddress(nameof(MiningGovernance), MiningGovernance);
            PersistentState.SetUInt32(nameof(IOpdexMinedToken.PeriodIndex), periodIndex);
            
            SetupBlock(genesis);

            token.Invoking(t => t.Distribute(new byte[0]))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: DISTRIBUTION_NOT_READY");
        }
        
        [Fact]
        public void Distribute_Throws_FailedVaultNotification()
        {
            const ulong genesis = 100;
            
            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule), genesis);
            
            var stakingTokens = Serializer.Serialize(new[] { Miner1, Pool1, ODX, Owner }); // Any 4 address, not important for this test
            
            SetupCall(MiningGovernance, 0ul, nameof(IOpdexMiningGovernance.NotifyDistribution), new object[] { stakingTokens }, TransferResult.Transferred(null));
            
            SetupCall(Vault, 0ul, nameof(IOpdexVault.NotifyDistribution), new object[] { DefaultVaultSchedule[0] }, TransferResult.Failed());

            PersistentState.SetAddress(nameof(MiningGovernance), MiningGovernance);
            
            SetupBlock(genesis);

            token.Invoking(t => t.Distribute(stakingTokens))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: FAILED_VAULT_DISTRIBUTION");
        }
        
        [Fact]
        public void Distribute_Throws_FailedGovernanceNotification()
        {
            const ulong genesis = 100;
            
            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule), genesis);
            
            var stakingTokens = Serializer.Serialize(new[] { Miner1, Pool1, ODX, Owner }); // Any 4 address, not important for this test
            
            SetupCall(MiningGovernance, 0ul, nameof(IOpdexMiningGovernance.NotifyDistribution), new object[] { stakingTokens }, TransferResult.Failed());
            
            PersistentState.SetAddress(nameof(MiningGovernance), MiningGovernance);
            
            SetupBlock(genesis);

            token.Invoking(t => t.Distribute(stakingTokens))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: FAILED_GOVERNANCE_DISTRIBUTION");
        }

        [Fact]
        public void NominateLiquidityMiningPool_Success()
        {
            const ulong genesis = 100;
            UInt256 weight = 1000;
            
            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule), genesis);

            SetupMessage(MiningGovernance, Pool1);
            
            PersistentState.SetContract(Pool1, true);
            PersistentState.SetUInt256($"Balance:{Pool1}", weight);

            var notifyParams = new object[] {Pool1, weight};
            
            SetupCall(MiningGovernance, 0ul, nameof(IOpdexMiningGovernance.NominateLiquidityPool), notifyParams, TransferResult.Transferred(null));

            token.NominateLiquidityPool();

            VerifyCall(MiningGovernance, 0ul, nameof(IOpdexMiningGovernance.NominateLiquidityPool), notifyParams, Times.Once);
        }
        
        [Fact]
        public void NominateLiquidityMiningPool_Throws_SenderIsNotContract()
        {
            const ulong genesis = 100;
            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule), genesis);

            SetupMessage(MiningGovernance, Miner1);

            token.Invoking(t => t.NominateLiquidityPool())
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_SENDER");
        }
        
        [Fact]
        public void NominateLiquidityMiningPool_FailsSilent_ZeroBalance()
        {
            const ulong genesis = 100;
            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule), genesis);

            SetupMessage(MiningGovernance, Pool1);
            
            PersistentState.SetContract(Pool1, true);
            PersistentState.SetUInt256($"Balance:{Pool1}", UInt256.Zero);

            var notifyParams = new object[] {Pool1, UInt256.Zero};
            
            token.NominateLiquidityPool();

            VerifyCall(MiningGovernance, 0ul, nameof(IOpdexMiningGovernance.NominateLiquidityPool), notifyParams, Times.Never);
        }

        [Theory]
        [InlineData(100, 50)]
        [InlineData(100, 0)]
        public void TransferFrom_Success(UInt256 ownerBalance, UInt256 spenderAllowance)
        {
            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule));

            SetupMessage(MiningGovernance, Pool1);
            
            PersistentState.SetUInt256($"Balance:{Miner1}", ownerBalance);
            PersistentState.SetUInt256($"Allowance:{Miner1}:{Pool1}", spenderAllowance);

            token.TransferFrom(Miner1, Owner, spenderAllowance).Should().BeTrue();

            VerifyLog(new TransferLog
            {
                From = Miner1,
                To = Owner,
                Amount = spenderAllowance
            }, Times.Once);
        }
        
        [Fact]
        public void TransferFrom_Fails_InvalidAllowance()
        {
            UInt256 ownerBalance = 100;
            UInt256 spenderAllowance = 150;
            
            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule));

            SetupMessage(MiningGovernance, Miner1);
            
            PersistentState.SetUInt256($"Balance:{Owner}", ownerBalance);
            PersistentState.SetUInt256($"Allowance:{Owner}:{Miner1}", spenderAllowance);

            token.TransferFrom(Owner, Miner1, spenderAllowance + 1).Should().BeFalse();
            token.GetBalance(Owner).Should().Be(ownerBalance);
            token.Allowance(Owner, Miner1).Should().Be(spenderAllowance);

            VerifyLog(new TransferLog
            {
                From = Miner1,
                To = Owner,
                Amount = spenderAllowance
            }, Times.Never);
        }
        
        [Fact]
        public void TransferFrom_Fails_InvalidOwnerAmount()
        {
            UInt256 ownerBalance = 100;
            UInt256 spenderAllowance = 150;
            
            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule));

            SetupMessage(MiningGovernance, Pool1);
            
            PersistentState.SetUInt256($"Balance:{Miner1}", ownerBalance);
            PersistentState.SetUInt256($"Allowance:{Miner1}:{Pool1}", spenderAllowance);

            token.TransferFrom(Miner1, Owner, spenderAllowance).Should().BeFalse();

            VerifyLog(new TransferLog
            {
                From = Miner1,
                To = Owner,
                Amount = spenderAllowance
            }, Times.Never);
        }
        
        [Theory]
        [InlineData(100, 50)]
        [InlineData(100, 0)]
        public void TransferTo_Success(UInt256 ownerBalance, UInt256 transferAmount)
        {
            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule));

            SetupMessage(MiningGovernance, Miner1);
            
            PersistentState.SetUInt256($"Balance:{Miner1}", ownerBalance);

            token.TransferTo(Owner, transferAmount).Should().BeTrue();

            VerifyLog(new TransferLog
            {
                From = Miner1,
                To = Owner,
                Amount = transferAmount
            }, Times.Once);
        }
        
        [Fact]
        public void TransferTo_Fails_InvalidOwnerAmount()
        {
            UInt256 ownerBalance = 100;
            UInt256 transferAmount = 150;
            
            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule));

            SetupMessage(MiningGovernance, Miner1);
            
            PersistentState.SetUInt256($"Balance:{Miner1}", ownerBalance);

            token.TransferTo(Owner, transferAmount).Should().BeFalse();

            VerifyLog(new TransferLog
            {
                From = Miner1,
                To = Owner,
                Amount = transferAmount
            }, Times.Never);
        }

        [Fact]
        public void Approve_Success()
        {
            UInt256 ownerBalance = 100;
            UInt256 currentAmount = 50;
            UInt256 amount = 100;
            
            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule));

            SetupMessage(MiningGovernance, Miner1);
            
            PersistentState.SetUInt256($"Balance:{Miner1}", ownerBalance);
            PersistentState.SetUInt256($"Allowance:{Miner1}:{Owner}", currentAmount);

            token.Approve(Owner, currentAmount, amount).Should().BeTrue();

            VerifyLog(new ApprovalLog
            {
                Owner = Miner1,
                Spender = Owner,
                Amount = amount,
                OldAmount = currentAmount
            }, Times.Once);
        }

        [Fact]
        public void Approve_Fail_InvalidCurrentAmount()
        {
            UInt256 ownerBalance = 100;
            UInt256 currentAmount = 50;
            UInt256 amount = 100;
            
            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule));

            SetupMessage(MiningGovernance, Miner1);
            
            PersistentState.SetUInt256($"Balance:{Miner1}", ownerBalance);
            PersistentState.SetUInt256($"Allowance:{Miner1}:{Owner}", 0);

            token.Approve(Owner, currentAmount, amount).Should().BeFalse();

            VerifyLog(new ApprovalLog
            {
                Owner = Miner1,
                Spender = Owner,
                Amount = amount,
                OldAmount = currentAmount
            }, Times.Never);
        }

        [Fact]
        public void Serialize_Distribution_Schedules()
        {
            _testOutputHelper.WriteLine(Serializer.Serialize(DefaultVaultSchedule).ToHexString());
            _testOutputHelper.WriteLine(Serializer.Serialize(DefaultMiningSchedule).ToHexString());
        }

        [Theory]
        [InlineData("4E6F6D696E6174696F6E4C6F67", "NominationLog")]
        public void DeserializeHexString_Success(string hex, string expectedOutput)
        {
            var bytes = hex.HexToByteArray();
            var output = Serializer.ToString(bytes);
            
            _testOutputHelper.WriteLine(output);
            output.Should().Be(expectedOutput);
        }
    }
}