using System.Linq;
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

            vault.Owner.Should().Be(Owner);
            vault.Token.Should().Be(ODX);
            vault.Genesis.Should().Be(10ul);
            vault.VestingDuration.Should().Be(BlocksPerYear * 4);
            vault.GetCertificates(Owner).Length.Should().Be(0);
        }

        [Theory]
        [InlineData(0, 100, 100)]
        [InlineData(100, 100, 200)]
        public void NotifyDistribution_Success(UInt256 currentTotalSupply, UInt256 distributionAmount, UInt256 expectedTotalSupply)
        {
            const ulong block = 100;
            
            var vault = CreateNewOpdexVault(block);
            
            PersistentState.SetUInt256(nameof(IOpdexVault.TotalSupply), currentTotalSupply);
            
            SetupMessage(Vault, ODX);
            
            vault.NotifyDistribution(distributionAmount);

            vault.TotalSupply.Should().Be(expectedTotalSupply);
        }

        [Fact]
        public void NotifyDistribution_Throws_Unauthorized()
        {
            var vault = CreateNewOpdexVault();
            
            SetupMessage(Vault, Miner1);

            vault
                .Invoking(v => v.NotifyDistribution(UInt256.MaxValue))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }

        [Fact]
        public void RedeemCertificates_None_Success()
        {
            const ulong block = 1000;
            
            var vault = CreateNewOpdexVault(block);
            
            PersistentState.SetArray($"Certificates:{Owner}", new VaultCertificate[0]);

            SetupMessage(Vault, Owner);
            
            vault.RedeemCertificates();

            vault.GetCertificates(Owner).Should().BeEquivalentTo(new VaultCertificate[0]);

            VerifyCall(ODX, 0, nameof(IOpdexMinedToken.TransferTo), It.IsAny<object[]>(), Times.Never);
            VerifyLog(It.IsAny<RedeemVaultCertificateLog>(), Times.Never);
        }
        
        [Fact]
        public void RedeemCertificates_SingleValid_Success()
        {
            const ulong block = 1000;

            var existingCertificate = new VaultCertificate {Amount = 100, VestedBlock = 500};
            
            var vault = CreateNewOpdexVault(block);
            
            PersistentState.SetArray($"Certificates:{Owner}", new[] { existingCertificate });

            SetupMessage(Vault, Owner);

            var transferToParams = new object[] {Owner, existingCertificate.Amount};
            SetupCall(ODX, 0, nameof(IOpdexMinedToken.TransferTo), transferToParams, TransferResult.Transferred(true));
            
            vault.RedeemCertificates();

            vault.GetCertificates(Owner).Length.Should().Be(0);

            VerifyCall(ODX, 0, nameof(IOpdexMinedToken.TransferTo), transferToParams, Times.Once);
            VerifyLog(new RedeemVaultCertificateLog {Owner = Owner, Amount = existingCertificate.Amount, VestedBlock = 500 }, Times.Once);
        }
        
        [Fact]
        public void RedeemCertificates_SingleInvalid_Success()
        {
            const ulong block = 1000;

            var existingCertificate = new VaultCertificate {Amount = 100, VestedBlock = block + 1};
            
            var vault = CreateNewOpdexVault(block);
            
            PersistentState.SetArray($"Certificates:{Owner}", new[] { existingCertificate });

            SetupMessage(Vault, Owner);
            
            vault.RedeemCertificates();

            vault.GetCertificates(Owner).Single().Should().BeEquivalentTo(existingCertificate);

            VerifyCall(ODX, 0, nameof(IOpdexMinedToken.TransferTo), It.IsAny<object[]>(), Times.Never);
            VerifyLog(It.IsAny<RedeemVaultCertificateLog>(), Times.Never);
        }
        
        [Fact]
        public void RedeemCertificates_MultipleValid_Success()
        {
            const ulong block = 3001;
            UInt256 expectedTotalTransfer = 175;

            var existingCertificates = new[]
            {
                new VaultCertificate {Amount = 100, VestedBlock = 1000},
                new VaultCertificate {Amount = 50, VestedBlock = 2000},
                new VaultCertificate {Amount = 25, VestedBlock = 3000},
                new VaultCertificate {Amount = 25, VestedBlock = 4000},
                new VaultCertificate {Amount = 25, VestedBlock = 5000},
                new VaultCertificate {Amount = 25, VestedBlock = 6000},
                new VaultCertificate {Amount = 25, VestedBlock = 7000},
                new VaultCertificate {Amount = 25, VestedBlock = 8000},
                new VaultCertificate {Amount = 25, VestedBlock = 9000},
                new VaultCertificate {Amount = 25, VestedBlock = 10000}
            };
            
            var vault = CreateNewOpdexVault(block);
            
            PersistentState.SetArray($"Certificates:{Owner}", existingCertificates);

            SetupMessage(Vault, Owner);

            var transferToParams = new object[] {Owner, expectedTotalTransfer};
            SetupCall(ODX, 0, nameof(IOpdexMinedToken.TransferTo), transferToParams, TransferResult.Transferred(true));
            
            vault.RedeemCertificates();

            var certificates = vault.GetCertificates(Owner);
            certificates.Length.Should().Be(7);
            certificates[0].Should().BeEquivalentTo(existingCertificates[3]);
            certificates[1].Should().BeEquivalentTo(existingCertificates[4]);
            certificates[2].Should().BeEquivalentTo(existingCertificates[5]);
            certificates[3].Should().BeEquivalentTo(existingCertificates[6]);
            certificates[4].Should().BeEquivalentTo(existingCertificates[7]);
            certificates[5].Should().BeEquivalentTo(existingCertificates[8]);
            certificates[6].Should().BeEquivalentTo(existingCertificates[9]);

            VerifyCall(ODX, 0, nameof(IOpdexMinedToken.TransferTo), transferToParams, Times.Once);
            VerifyLog(new RedeemVaultCertificateLog {Owner = Owner, Amount = existingCertificates[0].Amount, VestedBlock = existingCertificates[0].VestedBlock }, Times.Once);
            VerifyLog(new RedeemVaultCertificateLog {Owner = Owner, Amount = existingCertificates[1].Amount, VestedBlock = existingCertificates[1].VestedBlock }, Times.Once);
        }
        
        [Fact]
        public void RedeemCertificates_NoneValid_Success()
        {
            const ulong block = 100;

            var existingCertificates = new[]
            {
                new VaultCertificate {Amount = 100, VestedBlock = 500},
                new VaultCertificate {Amount = 50, VestedBlock = 1500}
            };
            
            var vault = CreateNewOpdexVault(block);
            
            PersistentState.SetArray($"Certificates:{Owner}", existingCertificates);

            SetupMessage(Vault, Owner);
            
            vault.RedeemCertificates();

            vault.GetCertificates(Owner).Should().BeEquivalentTo(existingCertificates);

            VerifyCall(ODX, 0, nameof(IOpdexMinedToken.TransferTo), It.IsAny<object[]>(), Times.Never);
            VerifyLog(It.IsAny<RedeemVaultCertificateLog>(), Times.Never);
        }
        
        [Fact]
        public void RedeemCertificates_AllValid_Success()
        {
            const ulong block = 2500;
            UInt256 expectedTotalTransfer = 150;

            var existingCertificates = new[]
            {
                new VaultCertificate {Amount = 100, VestedBlock = 500},
                new VaultCertificate {Amount = 50, VestedBlock = 1500}
            };
            
            var vault = CreateNewOpdexVault(block);
            
            PersistentState.SetArray($"Certificates:{Owner}", existingCertificates);

            SetupMessage(Vault, Owner);

            var transferToParams = new object[] {Owner, expectedTotalTransfer};
            SetupCall(ODX, 0, nameof(IOpdexMinedToken.TransferTo), transferToParams, TransferResult.Transferred(true));
            
            vault.RedeemCertificates();

            var certificates = vault.GetCertificates(Owner);
            certificates.Length.Should().Be(0);

            VerifyCall(ODX, 0, nameof(IOpdexMinedToken.TransferTo), transferToParams, Times.Once);
            VerifyLog(new RedeemVaultCertificateLog {Owner = Owner, Amount = existingCertificates[0].Amount, VestedBlock = existingCertificates[0].VestedBlock }, Times.Once);
            VerifyLog(new RedeemVaultCertificateLog {Owner = Owner, Amount = existingCertificates[1].Amount, VestedBlock = existingCertificates[1].VestedBlock }, Times.Once);
        }

        [Fact]
        public void CreateCertificate_NewHolder_Success()
        {
            const ulong block = 2500;
            UInt256 currentTotalSupply = 100;
            UInt256 transferAmount = 25;
            UInt256 expectedTotalSupply = 75;
            const ulong expectedVestedBlock = block + (BlocksPerYear * 4);
            
            var vault = CreateNewOpdexVault(block);
            
            PersistentState.SetUInt256(nameof(IOpdexVault.TotalSupply), currentTotalSupply);

            SetupMessage(Vault, Owner);

            vault.CreateCertificate(Miner1, transferAmount);

            var minerCertificates = vault.GetCertificates(Miner1);
            minerCertificates.Single().Amount.Should().Be(transferAmount);
            minerCertificates.Single().VestedBlock.Should().Be(expectedVestedBlock);
            vault.TotalSupply.Should().Be(expectedTotalSupply);
            
            VerifyLog(new CreateVaultCertificateLog
            {
                Owner = Miner1, 
                Amount = transferAmount, 
                VestedBlock = expectedVestedBlock
            }, Times.Once);
        }

        [Fact]
        public void CreateCertificate_ExistingHolder_Success()
        {
            const ulong block = 2500;
            UInt256 currentTotalSupply = 100;
            UInt256 transferAmount = 25;
            UInt256 expectedTotalSupply = 75;
            const ulong expectedVestedBlock = block + (BlocksPerYear * 4);
            
            var existingMinerCertificates = new[] { new VaultCertificate {Amount = 100, VestedBlock = 3000} };
            
            var vault = CreateNewOpdexVault(block);
            
            PersistentState.SetArray($"Certificates:{Miner1}", existingMinerCertificates);
            PersistentState.SetUInt256(nameof(IOpdexVault.TotalSupply), currentTotalSupply);

            SetupMessage(Vault, Owner);

            vault.CreateCertificate(Miner1, transferAmount);

            var minerCertificates = vault.GetCertificates(Miner1);

            minerCertificates.Length.Should().Be(2);
            minerCertificates[0].Amount.Should().Be((UInt256)100);
            minerCertificates[0].VestedBlock.Should().Be(3000);
            minerCertificates[1].Amount.Should().Be(transferAmount);
            minerCertificates[1].VestedBlock.Should().Be(expectedVestedBlock);
            vault.TotalSupply.Should().Be(expectedTotalSupply);

            VerifyLog(new CreateVaultCertificateLog
            {
                Owner = Miner1, 
                Amount = transferAmount, 
                VestedBlock = expectedVestedBlock
            }, Times.Once);
        }

        [Fact]
        public void CreateCertificate_Throws_Unauthorized()
        {
            const ulong block = 2500;
            UInt256 transferAmount = 25;
            
            var vault = CreateNewOpdexVault(block);
            
            SetupMessage(Vault, Miner1);
            
            vault
                .Invoking(v => v.CreateCertificate(Miner1, transferAmount))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }
        
        [Fact]
        public void CreateCertificate_Throws_InvalidCertificateHolder()
        {
            const ulong block = 2500;
            UInt256 transferAmount = 25;
            
            var vault = CreateNewOpdexVault(block);
            
            SetupMessage(Vault, Owner);
            
            vault
                .Invoking(v => v.CreateCertificate(Owner, transferAmount))
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
            
            PersistentState.SetUInt256(nameof(IOpdexVault.TotalSupply), currentTotalSupply);
            
            SetupMessage(Vault, Owner);
            
            vault
                .Invoking(v => v.CreateCertificate(Miner1, transferAmount))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_AMOUNT");
        }
        
        [Fact]
        public void CreateCertificate_Throws_TokensBurned()
        {
            const ulong block = ulong.MaxValue;
            UInt256 transferAmount = 25;
            
            var vault = CreateNewOpdexVault(block);
           
            PersistentState.SetUInt256(nameof(IOpdexVault.TotalSupply), UInt256.MaxValue);

            SetupMessage(Vault, Owner);
            
            vault
                .Invoking(v => v.CreateCertificate(Miner1, transferAmount))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: TOKENS_BURNED");
        }
        
        [Fact]
        public void SetOwner_Success()
        {
            var vault = CreateNewOpdexVault();
            
            SetupMessage(ODX, Owner);
            
            vault.SetOwner(MiningGovernance);

            vault.Owner.Should().Be(MiningGovernance);

            VerifyLog(new ChangeVaultOwnerLog
            {
                From = Owner,
                To = MiningGovernance
            }, Times.Once);
        }
        
        [Fact]
        public void SetOwner_Throws_Unauthorized()
        {
            var vault = CreateNewOpdexVault();
            
            SetupMessage(Vault, Miner1);

            vault.Invoking(v => v.SetOwner(MiningGovernance))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }

        [Theory]
        [InlineData((ulong)(BlocksPerYear * 4 * .01m), 100, 1, BlocksPerYear * 4)] // vested 1% of the 4 years
        [InlineData((ulong)(BlocksPerYear * 4 * .25m), 100, 25, BlocksPerYear * 4)] // vested 25% of the 4 years
        [InlineData((ulong)(BlocksPerYear * 4 * .5m), 100, 50, BlocksPerYear * 4)] // vested 50% of the 4 years
        [InlineData((ulong)(BlocksPerYear * 4 * .75m), 100, 75, BlocksPerYear * 4)] // vested 75% of the 4 years
        [InlineData((ulong)(BlocksPerYear * 4 * .99m), 100, 99, BlocksPerYear * 4)] // vested 99% of the 4 years
        public void RevokeCertificates_Single(ulong block, UInt256 currentAmount, UInt256 expectedAmount, ulong vestedBlock)
        {
            var totalSupply = 100;
            var existingMinerCertificates = new[] { new VaultCertificate {Amount = currentAmount, VestedBlock = vestedBlock} };
            
            var vault = CreateNewOpdexVault(block);
            
            PersistentState.SetArray($"Certificates:{Miner1}", existingMinerCertificates);
            PersistentState.SetUInt256(nameof(IOpdexVault.TotalSupply), totalSupply);
            
            SetupMessage(Vault, Owner);

            vault.RevokeCertificates(Miner1);
            
            vault.TotalSupply.Should().Be(totalSupply + (currentAmount - expectedAmount));

            var minerCertificates = vault.GetCertificates(Miner1);

            minerCertificates.Single().Amount.Should().Be(expectedAmount);
            minerCertificates.Single().VestedBlock.Should().Be(vestedBlock);
            minerCertificates.Single().Revoked.Should().BeTrue();

            VerifyLog(new RevokeVaultCertificateLog
            {
                Owner = Miner1, 
                OldAmount = currentAmount, 
                NewAmount = expectedAmount, 
                VestedBlock = vestedBlock
            }, Times.Once);
        }
        
        [Fact]
        public void RevokeCertificates_Multiple()
        {
            const ulong block = BlocksPerYear * 4;
            UInt256 currentTotalSupply = 100;
            UInt256 expectedTotalSupply = 200;
            UInt256 certOneCurrentAmount = 100;
            UInt256 certOneExpectedAmount = 75;
            const ulong certOneVestedBlock = BlocksPerYear * 5;
            UInt256 certTwoCurrentAmount = 100;
            UInt256 certTwoExpectedAmount = 25;
            const ulong certTwoVestedBlock = BlocksPerYear * 7;
            
            var existingMinerCertificates = new[]
            {
                new VaultCertificate {Amount = certOneCurrentAmount, VestedBlock = certOneVestedBlock},
                new VaultCertificate {Amount = certTwoCurrentAmount, VestedBlock = certTwoVestedBlock}
            };
            
            var vault = CreateNewOpdexVault(block);
            
            PersistentState.SetArray($"Certificates:{Miner1}", existingMinerCertificates);
            PersistentState.SetUInt256(nameof(IOpdexVault.TotalSupply), currentTotalSupply);
            
            SetupMessage(Vault, Owner);

            vault.RevokeCertificates(Miner1);

            vault.TotalSupply.Should().Be(expectedTotalSupply);

            var minerCertificates = vault.GetCertificates(Miner1);

            minerCertificates.First().Amount.Should().Be(certOneExpectedAmount);
            minerCertificates.First().VestedBlock.Should().Be(certOneVestedBlock);
            minerCertificates.First().Revoked.Should().BeTrue();
            minerCertificates.Last().Amount.Should().Be(certTwoExpectedAmount);
            minerCertificates.Last().VestedBlock.Should().Be(certTwoVestedBlock);
            minerCertificates.Last().Revoked.Should().BeTrue();

            VerifyLog(new RevokeVaultCertificateLog
            {
                Owner = Miner1, 
                OldAmount = certOneCurrentAmount, 
                NewAmount = certOneExpectedAmount, 
                VestedBlock = certOneVestedBlock
            }, Times.Once);
            
            VerifyLog(new RevokeVaultCertificateLog
            {
                Owner = Miner1, 
                OldAmount = certTwoCurrentAmount, 
                NewAmount = certTwoExpectedAmount, 
                VestedBlock = certTwoVestedBlock
            }, Times.Once);
        }

        [Fact]
        public void RevokeCertificates_Skip_Revoked()
        {
            UInt256 totalSupply = 100;
            const ulong block = BlocksPerYear * 4;
            UInt256 certOneCurrentAmount = 100;
            const ulong certOneVestedBlock = BlocksPerYear * 5;
            
            var existingMinerCertificates = new[]
            {
                new VaultCertificate {Amount = certOneCurrentAmount, VestedBlock = certOneVestedBlock, Revoked = true}
            };
            
            var vault = CreateNewOpdexVault(block);
            
            PersistentState.SetArray($"Certificates:{Miner1}", existingMinerCertificates);
            PersistentState.SetUInt256(nameof(IOpdexVault.TotalSupply), totalSupply);

            SetupMessage(Vault, Owner);

            vault.RevokeCertificates(Miner1);

            vault.TotalSupply.Should().Be(totalSupply);

            var minerCertificates = vault.GetCertificates(Miner1);

            minerCertificates.Single().Amount.Should().Be(certOneCurrentAmount);
            minerCertificates.Single().VestedBlock.Should().Be(certOneVestedBlock);
            minerCertificates.Single().Revoked.Should().BeTrue();

            VerifyLog(It.IsAny<RevokeVaultCertificateLog>(), Times.Never);
        }
        
        [Fact]
        public void RevokeCertificates_Skip_Vested()
        {
            UInt256 totalSupply = 100;
            const ulong block = BlocksPerYear * 4;
            UInt256 certOneCurrentAmount = 100;
            
            var existingMinerCertificates = new[]
            {
                new VaultCertificate {Amount = certOneCurrentAmount, VestedBlock = block}
            };
            
            var vault = CreateNewOpdexVault(block);
            
            PersistentState.SetArray($"Certificates:{Miner1}", existingMinerCertificates);
            PersistentState.SetUInt256(nameof(IOpdexVault.TotalSupply), totalSupply);

            SetupMessage(Vault, Owner);

            vault.RevokeCertificates(Miner1);

            vault.TotalSupply.Should().Be(totalSupply);
            
            var minerCertificates = vault.GetCertificates(Miner1);

            minerCertificates.Single().Amount.Should().Be(certOneCurrentAmount);
            minerCertificates.Single().VestedBlock.Should().Be(block);
            minerCertificates.Single().Revoked.Should().BeFalse();

            VerifyLog(It.IsAny<RevokeVaultCertificateLog>(), Times.Never);
        }
    }
}