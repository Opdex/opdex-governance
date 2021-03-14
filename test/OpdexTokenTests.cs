using FluentAssertions;
using OpdexTokenTests.Base;
using Stratis.SmartContracts;
using Xunit;

namespace OpdexTokenTests
{
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
            token.TreasurySupply.Should().Be(UInt256.Zero);
            token.GetBalance(Owner).Should().Be(UInt256.Zero);
            token.GetBalance(Factory).Should().Be(UInt256.Zero);
        }
    }
}