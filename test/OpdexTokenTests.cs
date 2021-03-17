using FluentAssertions;
using OpdexTokenTests.Base;
using Stratis.SmartContracts;
using Xunit;

namespace OpdexTokenTests
{
    // private readonly UInt256[] _ownerSchedule = { 120_000_000, 90_000_000, 60_000_000, 30_000_000, 7_500_000 };
    // private readonly UInt256[] _miningSchedule = { 280_000_000, 210_000_000, 140_000_000, 70_000_000, 17_500_000 };
    public class OpdexTokenTests : TestBase
    {
        [Fact]
        public void CreatesContract_Success()
        {
            var token = CreateNewOpdexToken();

            token.Owner.Should().Be(Owner);
            token.Name.Should().Be("Opdex");
            token.Symbol.Should().Be("OPDX");
            token.Decimals.Should().Be(8);
            token.Genesis.Should().Be(10ul);
            token.TotalSupply.Should().Be(UInt256.Zero);
            token.GetBalance(Owner).Should().Be(UInt256.Zero);
            token.GetBalance(Factory).Should().Be(UInt256.Zero);
        }
    }
}