using FluentAssertions;
using Moq;
using OpdexTokenTests.Base;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace OpdexTokenTests
{
    public class OpdexMiningTests : TestBase
    {
        [Fact]
        public void CreatesContract_Success()
        {
            // const ulong duration = 100;
            // UInt256 amountToDistribute = 20_000_000_000_000_000;
            // UInt256 expectedBlockReward = 200_000_000_000_000;
            // UInt256 expectedDistributionTotal = 19_999_999_999_999_000;
            //
            // var miner = CreateNewOpdexMiner(amountToDistribute, duration);
            //
            // miner.EndBlock.Should().Be(110);
            // miner.BlockReward.Should().Be(expectedBlockReward);
            // miner.DistributionTotal.Should().Be(expectedDistributionTotal);
            // miner.DistributionAmount.Should().Be((UInt256)1000);
            // miner.Pair.Should().Be(Pair);
            // miner.OPDX.Should().Be(OPDX);
        }

        [Fact]
        public void Mine_Success()
        {
            // const ulong duration = 100;
            // UInt256 amountToDistribute = 20_000_000_000_000_000;
            // UInt256 amountToMine = 10;
            // UInt256 expectedWeightK = 100;
            //
            // PersistentState.SetUInt256($"Weight:{Miner}", 0);
            // PersistentState.SetUInt256($"WeightK:{Miner}", 0);
            // PersistentState.SetUInt256("TotalWeight", 0);
            //
            // var miner = CreateNewOpdexMiner(amountToDistribute, duration);
            //
            // SetupMessage(MiningContract, Miner);
            //
            // SetupCall(Pair, 0, "TransferFrom", new object[] { Miner, MiningContract, amountToMine }, TransferResult.Transferred(true));
            //
            // miner.Mine(amountToMine);
            //
            // VerifyLog(new OpdexMining.MineEvent {From = Miner, Weight = amountToMine, WeightK = expectedWeightK}, Times.Once);
        }
    }
}