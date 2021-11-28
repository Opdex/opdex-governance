using FluentAssertions;
using Moq;
using OpdexGovernanceTests.Base;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace OpdexGovernanceTests
{
    public class OpdexVaultTests : TestBase
    {
        [Fact]
        public void CreatesOpdexVault_Success()
        {
            var vault = CreateNewOpdexVault();

            vault.Governance.Should().Be(VaultGovernance);
            vault.Token.Should().Be(ODX);
            vault.Genesis.Should().Be(0);
            vault.VestingDuration.Should().Be(BlocksPerYear * 4);
        }

        #region Notify Distribution

        [Theory]
        [InlineData(0, 100, 100)]
        [InlineData(100, 100, 200)]
        public void NotifyDistribution_Success(UInt256 currentTotalSupply, UInt256 distributionAmount, UInt256 expectedTotalSupply)
        {
            var vault = CreateNewOpdexVault();

            State.SetUInt256(VaultStateKeys.TotalSupply, currentTotalSupply);

            SetupMessage(Vault, ODX);

            vault.NotifyDistribution(distributionAmount);

            vault.TotalSupply.Should().Be(expectedTotalSupply);
        }

        [Fact]
        public void NotifyDistribution_Throws_Unauthorized()
        {
            var vault = CreateNewOpdexVault();

            SetupMessage(Vault, VaultGovernance);

            vault
                .Invoking(v => v.NotifyDistribution(UInt256.MaxValue))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }

        #endregion

        #region Create Certificate

        [Fact]
        public void CreateCertificate_NewHolder_Success()
        {
            const ulong block = 2500;
            const ulong expectedVestedBlock = block + (BlocksPerYear * 4);
            UInt256 currentTotalSupply = 100;
            UInt256 expectedTotalSupply = 75;
            UInt256 transferAmount = 25;

            var vault = CreateNewOpdexVault(block);

            State.SetUInt256(VaultStateKeys.TotalSupply, currentTotalSupply);

            SetupMessage(Vault, VaultGovernance);

            vault.CreateCertificate(Miner, transferAmount);

            var minerCertificate = vault.GetCertificate(Miner);
            minerCertificate.Amount.Should().Be(transferAmount);
            minerCertificate.VestedBlock.Should().Be(expectedVestedBlock);
            minerCertificate.Revoked.Should().BeFalse();
            vault.TotalSupply.Should().Be(expectedTotalSupply);

            VerifyLog(new CreateVaultCertificateLog
            {
                Owner = Miner,
                Amount = transferAmount,
                VestedBlock = expectedVestedBlock
            }, Times.Once);
        }

        // Todo: Should change test to throw instead of a certificate exists for the holder
        // [Fact]
        // public void CreateCertificate_ExistingHolder_Success()
        // {
        //     const ulong block = 2500;
        //     const ulong expectedVestedBlock = block + (BlocksPerYear * 4);
        //     UInt256 currentTotalSupply = 100;
        //     UInt256 expectedTotalSupply = 75;
        //     UInt256 transferAmount = 25;
        //
        //     var existingMinerCertificates = new[] { new VaultCertificate {Amount = 100, VestedBlock = 3000, Revoked = false} };
        //
        //     var vault = CreateNewOpdexVault(block);
        //
        //     State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", existingMinerCertificates);
        //     State.SetUInt256(VaultStateKeys.TotalSupply, currentTotalSupply);
        //
        //     SetupMessage(Vault, VaultGovernance);
        //
        //     vault.CreateCertificate(Miner, transferAmount);
        //
        //     var minerCertificate = vault.GetCertificate(Miner);
        //
        //     minerCertificates.Length.Should().Be(2);
        //     minerCertificates[0].Amount.Should().Be(existingMinerCertificates[0].Amount);
        //     minerCertificates[0].VestedBlock.Should().Be(existingMinerCertificates[0].VestedBlock);
        //     minerCertificates[1].Amount.Should().Be(transferAmount);
        //     minerCertificates[1].VestedBlock.Should().Be(expectedVestedBlock);
        //     vault.TotalSupply.Should().Be(expectedTotalSupply);
        //
        //     VerifyLog(new CreateVaultCertificateLog
        //     {
        //         Owner = Miner,
        //         Amount = transferAmount,
        //         VestedBlock = expectedVestedBlock
        //     }, Times.Once);
        // }

        [Fact]
        public void CreateCertificate_Throws_Unauthorized()
        {
            const ulong block = 2500;
            UInt256 transferAmount = 25;

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            vault
                .Invoking(v => v.CreateCertificate(Miner, transferAmount))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }

        [Fact]
        public void CreateCertificate_Throws_InvalidCertificateHolder()
        {
            const ulong block = 2500;
            UInt256 transferAmount = 25;

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, VaultGovernance);

            vault
                .Invoking(v => v.CreateCertificate(VaultGovernance, transferAmount))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_CERTIFICATE_HOLDER");
        }

        [Theory]
        [InlineData(0, 25)]
        [InlineData(25, 0)]
        public void CreateCertificate_Throws_ZeroAmount(UInt256 currentTotalSupply, UInt256 transferAmount)
        {
            const ulong block = 2500;

            var vault = CreateNewOpdexVault(block);

            State.SetUInt256(VaultStateKeys.TotalSupply, currentTotalSupply);

            SetupMessage(Vault, VaultGovernance);

            vault
                .Invoking(v => v.CreateCertificate(Miner, transferAmount))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_AMOUNT");
        }

        [Fact]
        public void CreateCertificate_Throws_TokensBurned()
        {
            const ulong block = ulong.MaxValue;
            UInt256 transferAmount = 25;

            var vault = CreateNewOpdexVault(block);

            State.SetUInt256(VaultStateKeys.TotalSupply, UInt256.MaxValue);

            SetupMessage(Vault, VaultGovernance);

            vault
                .Invoking(v => v.CreateCertificate(Miner, transferAmount))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: TOKENS_BURNED");
        }

        [Fact]
        public void CreateCertificate_Throws_CertificateExists()
        {
            const ulong block = 2500;
            UInt256 currentTotalSupply = 100;
            UInt256 transferAmount = 25;

            var existingMinerCertificate = new VaultCertificate { Amount = 10, Revoked = false, VestedBlock = 10000 };

            var vault = CreateNewOpdexVault(block);

            State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", existingMinerCertificate);
            State.SetUInt256(VaultStateKeys.TotalSupply, currentTotalSupply);

            SetupMessage(Vault, VaultGovernance);

            vault
                .Invoking(v => v.CreateCertificate(Miner, transferAmount))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: CERTIFICATE_EXISTS");
        }

        #endregion

        #region Redeem Certificate

        [Fact]
        public void RedeemCertificate_Success()
        {
            const ulong block = 1000;
            const ulong vestedBlock = block - 1;

            var existingCertificate = new VaultCertificate {Amount = 100, VestedBlock = vestedBlock};

            var vault = CreateNewOpdexVault(block);

            State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", existingCertificate);

            SetupMessage(Vault, Miner);

            var transferToParams = new object[] {Miner, existingCertificate.Amount};
            SetupCall(ODX, 0, nameof(IOpdexMinedToken.TransferTo), transferToParams, TransferResult.Transferred(true));

            vault.RedeemCertificate();

            vault.GetCertificate(Miner).Should().Be(default(VaultCertificate));

            VerifyCall(ODX, 0, nameof(IOpdexMinedToken.TransferTo), transferToParams, Times.Once);
            VerifyLog(new RedeemVaultCertificateLog {Owner = Miner, Amount = existingCertificate.Amount, VestedBlock = vestedBlock }, Times.Once);
        }

        [Fact]
        public void RedeemCertificate_Throws_NotFound()
        {
            const ulong block = 1000;

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            vault
                .Invoking(v => v.RedeemCertificate())
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: CERTIFICATE_NOT_FOUND");
        }

        [Fact]
        public void RedeemCertificate_Throws_Vesting()
        {
            const ulong block = 1000;

            var existingCertificate = new VaultCertificate {Amount = 100, VestedBlock = block + 1};

            var vault = CreateNewOpdexVault(block);

            State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", existingCertificate);

            SetupMessage(Vault, Miner);

            vault
                .Invoking(v => v.RedeemCertificate())
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: CERTIFICATE_VESTING");
        }

        #endregion

        #region Revoke Certicates

        [Theory]
        [InlineData((ulong)(BlocksPerYear * 4 * .01m), 100, 1)] // vested 1% of the 4 years
        [InlineData((ulong)(BlocksPerYear * 4 * .25m), 100, 25)] // vested 25% of the 4 years
        [InlineData((ulong)(BlocksPerYear * 4 * .5m), 100, 50)] // vested 50% of the 4 years
        [InlineData((ulong)(BlocksPerYear * 4 * .75m), 100, 75)] // vested 75% of the 4 years
        [InlineData((ulong)(BlocksPerYear * 4 * .99m), 100, 99)] // vested 99% of the 4 years
        public void RevokeCertificate_Success(ulong block, UInt256 currentAmount, UInt256 expectedAmount)
        {
            const ulong vestedBlock = BlocksPerYear * 4;
            UInt256 totalSupply = 100;
            var existingMinerCertificate = new VaultCertificate {Amount = currentAmount, VestedBlock = vestedBlock};

            var vault = CreateNewOpdexVault(block);

            State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", existingMinerCertificate);
            State.SetUInt256(VaultStateKeys.TotalSupply, totalSupply);

            SetupMessage(Vault, VaultGovernance);

            vault.RevokeCertificate(Miner);

            vault.TotalSupply.Should().Be(totalSupply + (currentAmount - expectedAmount));

            var minerCertificate = vault.GetCertificate(Miner);

            minerCertificate.Amount.Should().Be(expectedAmount);
            minerCertificate.VestedBlock.Should().Be(vestedBlock);
            minerCertificate.Revoked.Should().BeTrue();

            VerifyLog(new RevokeVaultCertificateLog
            {
                Owner = Miner,
                OldAmount = currentAmount,
                NewAmount = expectedAmount,
                VestedBlock = vestedBlock
            }, Times.Once);
        }

        [Fact]
        public void RevokeCertificate_Throws_CertificateVested()
        {
            UInt256 totalSupply = 100;
            const ulong block = BlocksPerYear * 4;
            UInt256 certOneCurrentAmount = 100;

            var existingMinerCertificate = new VaultCertificate { Amount = certOneCurrentAmount, VestedBlock = 1 };

            var vault = CreateNewOpdexVault(block);

            State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", existingMinerCertificate);
            State.SetUInt256(VaultStateKeys.TotalSupply, totalSupply);

            SetupMessage(Vault, VaultGovernance);

            vault
                .Invoking(v => v.RevokeCertificate(Miner))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: CERTIFICATE_VESTED");
        }

        [Fact]
        public void RevokeCertificate_Throws_PreviouslyRevoked()
        {
            UInt256 totalSupply = 100;
            const ulong block = BlocksPerYear * 4;
            UInt256 certOneCurrentAmount = 100;

            var existingMinerCertificate = new VaultCertificate { Amount = certOneCurrentAmount, VestedBlock = block, Revoked = true };

            var vault = CreateNewOpdexVault(block);

            State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", existingMinerCertificate);
            State.SetUInt256(VaultStateKeys.TotalSupply, totalSupply);

            SetupMessage(Vault, VaultGovernance);

            vault
                .Invoking(v => v.RevokeCertificate(Miner))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: CERTIFICATE_PREVIOUSLY_REVOKED");
        }

        [Fact]
        public void RevokeCertificate_Throws_Unauthorized()
        {
            var vault = CreateNewOpdexVault();

            SetupMessage(Vault, Miner);

            vault
                .Invoking(v => v.RevokeCertificate(Miner))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }

        #endregion
    }
}
