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

        // 100M, 75M, 50M, 25M, 0
        private readonly UInt256[] DefaultVaultSchedule =
        {
            10_000_000_000_000_000,
            7_500_000_000_000_000,
            5_000_000_000_000_000,
            2_500_000_000_000_000,
            0
        };

        // 300M, 225M, 150M, 75M, 25M
        private readonly UInt256[] DefaultMiningSchedule =
        {
            30_000_000_000_000_000,
            22_500_000_000_000_000,
            15_000_000_000_000_000,
            7_500_000_000_000_000,
            2_500_000_000_000_000
        };

        public OpdexTokenTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void CreateContract_Success()
        {
            var vaultSchedule = Serializer.Serialize(DefaultVaultSchedule);
            var miningSchedule = Serializer.Serialize(DefaultMiningSchedule);

            var token = CreateNewOpdexToken(vaultSchedule, miningSchedule);

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
            var vaultSchedule = Serializer.Serialize(new UInt256[] { 12345 });
            var miningSchedule = Serializer.Serialize(new UInt256[] { 9876 });

            var threw = false;

            this
                .Invoking(t => t.CreateNewOpdexToken(vaultSchedule, miningSchedule))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_DISTRIBUTION_SCHEDULE");
        }

        [Fact]
        public void CreateContract_Throws_DifferentScheduleLengths()
        {
            var vaultSchedule = Serializer.Serialize(new UInt256[] { 125 });
            var miningSchedule = Serializer.Serialize(new UInt256[0]);

            this
                .Invoking(t => t.CreateNewOpdexToken(vaultSchedule, miningSchedule))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_DISTRIBUTION_SCHEDULE");
        }

        #region Distribute Genesis

        [Fact]
        public void DistributeGenesis_Success()
        {
            const ulong block = 100;

            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule));

            State.SetContract(Pool1, true);
            State.SetContract(Pool2, true);
            State.SetContract(Pool3, true);
            State.SetContract(Pool4, true);

            var governanceCallParams = new object[] { Pool1, Pool2, Pool3, Pool4 };
            SetupCall(MiningGovernance, 0ul, nameof(IOpdexMiningGovernance.NotifyDistribution), governanceCallParams, TransferResult.Transferred(null));

            var vaultCallParams = new object[] {DefaultVaultSchedule[0]};
            SetupCall(Vault, 0ul, nameof(IOpdexVault.NotifyDistribution), vaultCallParams, TransferResult.Transferred(null));

            SetupBlock(block);

            token.DistributeGenesis(Pool1, Pool2, Pool3, Pool4);

            token.GetBalance(Vault).Should().Be(DefaultVaultSchedule[0]);
            token.GetBalance(MiningGovernance).Should().Be(DefaultMiningSchedule[0]);
            token.PeriodIndex.Should().Be(1);
            token.TotalSupply.Should().Be(DefaultMiningSchedule[0] + DefaultVaultSchedule[0]);
            token.Genesis.Should().Be(block);

            VerifyCall(MiningGovernance, 0ul, nameof(IOpdexMiningGovernance.NotifyDistribution), governanceCallParams, Times.Once);
            VerifyCall(Vault, 0ul, nameof(IOpdexVault.NotifyDistribution), vaultCallParams, Times.Once);

            VerifyLog(new DistributionLog
            {
                VaultAmount = DefaultVaultSchedule[0],
                MiningAmount = DefaultMiningSchedule[0],
                PeriodIndex = 0
            }, Times.Once);
        }

        [Fact]
        public void DistributeGenesis_Throws_InvalidDistributionPeriod()
        {
            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule));

            State.SetUInt32(TokenStateKeys.PeriodIndex, 1);

            token
                .Invoking(t => t.DistributeGenesis(Pool1, Pool2, Pool3, Pool4))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_DISTRIBUTION_PERIOD");
        }

        [Fact]
        public void DistributeGenesis_Throws_Unauthorized()
        {
            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule));

            SetupMessage(ODX, Miner);

            token
                .Invoking(t => t.DistributeGenesis(Pool1, Pool2, Pool3, Pool4))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }

        [Fact]
        public void DistributeGenesis_Throws_InvalidNomination()
        {
            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule));

            SetupMessage(ODX, Owner);

            token
                .Invoking(t => t.DistributeGenesis(Pool1, Pool2, Pool3, Pool4))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_NOMINATION");
        }

        #endregion

        #region Distribute
        [Theory]
        [InlineData(1, 10_000_000_000_000_000, 30_000_000_000_000_000, 40_000_000_000_000_000, 17_500_000_000_000_000, 52_500_000_000_000_000, 70_000_000_000_000_000)]
        [InlineData(2, 17_500_000_000_000_000, 52_500_000_000_000_000, 70_000_000_000_000_000, 22_500_000_000_000_000, 67_500_000_000_000_000, 90_000_000_000_000_000)]
        [InlineData(3, 22_500_000_000_000_000, 67_500_000_000_000_000, 90_000_000_000_000_000, 25_000_000_000_000_000, 75_000_000_000_000_000, 1_00_000_000_000_000_000)]
        [InlineData(4, 25_000_000_000_000_000, 75_000_000_000_000_000, 100_000_000_000_000_000, 25_000_000_000_000_000, 77_500_000_000_000_000, 102_500_000_000_000_000)]
        [InlineData(5, 25_000_000_000_000_000, 77_500_000_000_000_000, 102_500_000_000_000_000, 25_000_000_000_000_000, 80_000_000_000_000_000, 1_05_000_000_000_000_000)]
        [InlineData(6, 25_000_000_000_000_000, 80_000_000_000_000_000, 1_05_000_000_000_000_000, 25_000_000_000_000_000, 82_500_000_000_000_000, 107_500_000_000_000_000)]
        public void Distribute_Success(uint periodIndex, UInt256 currentVaultBalance, UInt256 currentMiningBalance, UInt256 currentTotalSupply,
            UInt256 expectedVaultBalance, UInt256 expectedMiningBalance, UInt256 expectedTotalSupply)
        {
            const ulong genesis = 100;
            const ulong periodDuration = 1000;

            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule), genesis);

            State.SetUInt256($"{TokenStateKeys.Balance}:{Vault}", currentVaultBalance);
            State.SetUInt256($"{TokenStateKeys.Balance}:{MiningGovernance}", currentMiningBalance);
            State.SetUInt32(TokenStateKeys.PeriodIndex, periodIndex);
            State.SetUInt64(TokenStateKeys.PeriodDuration, periodDuration);
            State.SetUInt256(TokenStateKeys.TotalSupply, currentTotalSupply);
            State.SetUInt64(TokenStateKeys.Genesis, genesis);

            var block = (BlocksPerYear * periodIndex) + genesis;
            SetupBlock(block);

            var notifyDistributionParameters = new object[] { Address.Zero, Address.Zero, Address.Zero, Address.Zero };
            SetupCall(MiningGovernance, 0, nameof(IOpdexMiningGovernance.NotifyDistribution), notifyDistributionParameters, TransferResult.Transferred(null));

            var notifyVaultParams = new object[] {expectedVaultBalance - currentVaultBalance};
            SetupCall(Vault, 0, nameof(IOpdexVault.NotifyDistribution), notifyVaultParams, TransferResult.Transferred(null));

            token.Distribute();

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
        public void Distribute_Throws_NotReady(uint periodIndex)
        {
            const ulong genesis = 100;

            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule), genesis);

            State.SetAddress(nameof(MiningGovernance), MiningGovernance);
            State.SetUInt32(TokenStateKeys.PeriodIndex, periodIndex);

            SetupBlock(genesis);

            token.Invoking(t => t.Distribute())
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: DISTRIBUTION_NOT_READY");
        }

        [Fact]
        public void Distribute_Throws_FailedVaultNotification()
        {
            const ulong genesis = 100;

            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule), genesis);

            SetupCall(MiningGovernance, 0ul, nameof(IOpdexMiningGovernance.NotifyDistribution), new object[] { Pool1, Pool2, Pool3, Pool4 }, TransferResult.Transferred(null));

            SetupCall(Vault, 0ul, nameof(IOpdexVault.NotifyDistribution), new object[] { DefaultVaultSchedule[0] }, TransferResult.Failed());

            State.SetAddress(nameof(MiningGovernance), MiningGovernance);
            State.SetContract(Pool1, true);
            State.SetContract(Pool2, true);
            State.SetContract(Pool3, true);
            State.SetContract(Pool4, true);

            SetupBlock(genesis);

            token.Invoking(t => t.DistributeGenesis(Pool1, Pool2, Pool3, Pool4))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: FAILED_VAULT_DISTRIBUTION");
        }

        [Fact]
        public void Distribute_Throws_FailedGovernanceNotification()
        {
            const ulong genesis = 100;

            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule), genesis);

            SetupCall(MiningGovernance, 0ul, nameof(IOpdexMiningGovernance.NotifyDistribution), new object[] { Pool1, Pool2, Pool3, Pool4 }, TransferResult.Failed());

            State.SetAddress(nameof(MiningGovernance), MiningGovernance);
            State.SetContract(Pool1, true);
            State.SetContract(Pool2, true);
            State.SetContract(Pool3, true);
            State.SetContract(Pool4, true);

            SetupBlock(genesis);

            token.Invoking(t => t.DistributeGenesis(Pool1, Pool2, Pool3, Pool4))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: FAILED_GOVERNANCE_DISTRIBUTION");
        }

        [Fact]
        public void Distribute_Throws_InvalidDistributionPeriod()
        {
            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule));

            State.SetUInt32(TokenStateKeys.PeriodIndex, 0);

            token
                .Invoking(t => t.Distribute())
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_DISTRIBUTION_PERIOD");
        }

        #endregion

        #region Nominate Liquidity Pool

        [Fact]
        public void NominateLiquidityMiningPool_Success()
        {
            const ulong genesis = 100;
            UInt256 weight = 1000;

            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule), genesis);

            SetupMessage(MiningGovernance, Pool1);

            State.SetContract(Pool1, true);
            State.SetUInt256($"{TokenStateKeys.Balance}:{Pool1}", weight);

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

            SetupMessage(MiningGovernance, Miner);

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

            State.SetContract(Pool1, true);
            State.SetUInt256($"{TokenStateKeys.Balance}:{Pool1}", UInt256.Zero);

            var notifyParams = new object[] {Pool1, UInt256.Zero};

            token.NominateLiquidityPool();

            VerifyCall(MiningGovernance, 0ul, nameof(IOpdexMiningGovernance.NominateLiquidityPool), notifyParams, Times.Never);
        }

        [Fact]
        public void NominateLiquidityMiningPool_FailsSilent_FailedGovernanceCall()
        {
            const ulong genesis = 100;
            UInt256 weight = 1000;

            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule), genesis);

            SetupMessage(MiningGovernance, Pool1);

            State.SetContract(Pool1, true);
            State.SetUInt256($"{TokenStateKeys.Balance}:{Pool1}", weight);

            var notifyParams = new object[] {Pool1, weight};
            SetupCall(MiningGovernance, 0ul, nameof(IOpdexMiningGovernance.NominateLiquidityPool), notifyParams, TransferResult.Failed());

            token.NominateLiquidityPool();

            VerifyCall(MiningGovernance, 0ul, nameof(IOpdexMiningGovernance.NominateLiquidityPool), notifyParams, Times.Once);
        }

        #endregion

        #region Transfer From

        [Theory]
        [InlineData(100, 50)]
        [InlineData(100, 0)]
        public void TransferFrom_Success(UInt256 ownerBalance, UInt256 spenderAllowance)
        {
            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule));

            var owner = Miner;
            var spender = Pool1;
            var recipient = MiningGovernance;

            SetupMessage(ODX, spender);

            State.SetUInt256($"{TokenStateKeys.Balance}:{owner}", ownerBalance);
            State.SetUInt256($"{TokenStateKeys.Allowance}:{owner}:{spender}", spenderAllowance);

            token.TransferFrom(owner, recipient, spenderAllowance).Should().BeTrue();
            token.GetBalance(owner).Should().Be(ownerBalance - spenderAllowance);
            token.Allowance(owner, spender).Should().Be(UInt256.Zero);

            VerifyLog(new TransferLog { From = owner, To = recipient, Amount = spenderAllowance }, Times.Once);
        }

        [Fact]
        public void TransferFrom_Fails_InvalidAllowance()
        {
            UInt256 ownerBalance = 100;
            UInt256 spenderAllowance = 150;
            UInt256 spenderAttempt = 151;
            var owner = Miner;
            var spender = Pool1;
            var recipient = MiningGovernance;

            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule));

            SetupMessage(ODX, spender);

            State.SetUInt256($"{TokenStateKeys.Balance}:{owner}", ownerBalance);
            State.SetUInt256($"{TokenStateKeys.Allowance}:{owner}:{spender}", spenderAllowance);

            token.TransferFrom(owner, recipient, spenderAttempt).Should().BeFalse();
            token.GetBalance(owner).Should().Be(ownerBalance);
            token.Allowance(owner, spender).Should().Be(spenderAllowance);

            VerifyLog(It.IsAny<TransferLog>(), Times.Never);
        }

        [Fact]
        public void TransferFrom_Fails_InvalidOwnerAmount()
        {
            UInt256 ownerBalance = 100;
            UInt256 spenderAllowance = 150;
            var owner = Miner;
            var spender = Pool1;
            var recipient = MiningGovernance;

            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule));

            SetupMessage(ODX, spender);

            State.SetUInt256($"{TokenStateKeys.Balance}:{owner}", ownerBalance);
            State.SetUInt256($"{TokenStateKeys.Allowance}:{owner}:{spender}", spenderAllowance);

            token.TransferFrom(owner, recipient, spenderAllowance).Should().BeFalse();
            token.GetBalance(owner).Should().Be(ownerBalance);
            token.Allowance(owner, spender).Should().Be(spenderAllowance);

            VerifyLog(It.IsAny<TransferLog>(), Times.Never);
        }

        #endregion

        #region Transfer To

        [Theory]
        [InlineData(100, 50)]
        [InlineData(100, 0)]
        public void TransferTo_Success(UInt256 ownerBalance, UInt256 transferAmount)
        {
            var existingMinerBalance = 25;
            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule));

            SetupMessage(ODX, Owner);

            State.SetUInt256($"{TokenStateKeys.Balance}:{Owner}", ownerBalance);
            State.SetUInt256($"{TokenStateKeys.Balance}:{Miner}", existingMinerBalance);

            token.TransferTo(Miner, transferAmount).Should().BeTrue();
            token.GetBalance(Owner).Should().Be(ownerBalance - transferAmount);
            token.GetBalance(Miner).Should().Be(transferAmount + existingMinerBalance);

            VerifyLog(new TransferLog { From = Owner, To = Miner, Amount = transferAmount }, Times.Once);
        }

        [Fact]
        public void TransferTo_Fails_InvalidOwnerAmount()
        {
            UInt256 ownerBalance = 100;
            UInt256 transferAmount = 150;

            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule));

            SetupMessage(ODX, Owner);

            State.SetUInt256($"{TokenStateKeys.Balance}:{Owner}", ownerBalance);

            token.TransferTo(Owner, transferAmount).Should().BeFalse();
            token.GetBalance(Owner).Should().Be(ownerBalance);

            VerifyLog(It.IsAny<TransferLog>(), Times.Never);
        }

        #endregion

        #region Approve

        [Fact]
        public void Approve_Success()
        {
            UInt256 ownerBalance = 100;
            UInt256 currentAmount = 50;
            UInt256 amount = 100;
            var owner = Owner;
            var spender = Miner;

            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule));

            SetupMessage(ODX, owner);

            State.SetUInt256($"{TokenStateKeys.Balance}:{owner}", ownerBalance);
            State.SetUInt256($"{TokenStateKeys.Allowance}:{owner}:{spender}", currentAmount);

            token.Approve(spender, currentAmount, amount).Should().BeTrue();
            token.Allowance(owner, spender).Should().Be(amount);

            VerifyLog(new ApprovalLog { Owner = owner, Spender = spender, Amount = amount, OldAmount = currentAmount }, Times.Once);
        }

        [Fact]
        public void Approve_Fail_InvalidCurrentAmount()
        {
            UInt256 ownerBalance = 100;
            UInt256 currentAmount = 50;
            UInt256 amount = 100;
            var owner = Owner;
            var spender = Miner;

            var token = CreateNewOpdexToken(Serializer.Serialize(DefaultVaultSchedule), Serializer.Serialize(DefaultMiningSchedule));

            SetupMessage(ODX, owner);

            State.SetUInt256($"{TokenStateKeys.Balance}:{owner}", ownerBalance);
            State.SetUInt256($"{TokenStateKeys.Allowance}:{owner}:{spender}", 0);

            token.Approve(spender, currentAmount, amount).Should().BeFalse();
            token.Allowance(owner, spender).Should().Be(UInt256.Zero);

            VerifyLog(It.IsAny<TransferLog>(), Times.Never);
        }

        #endregion

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