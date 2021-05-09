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
        public void CreatesOpdexValue_Success()
        {
            var vault = CreateNewOpdexVault();

            vault.Owner.Should().Be(Owner);
            vault.Token.Should().Be(ODX);
            vault.GetCertificates(Owner).Length.Should().Be(0);
        }

        [Fact]
        public void NotifyDistribution_NoExistingCertificates_Success()
        {
            UInt256 distributionAmount = 100;
            const ulong block = 100;
            
            var expectedCertificate = new VaultCertificate {Amount = distributionAmount, VestedBlock = block + BlocksPerYear};

            var vault = CreateNewOpdexVault(block);
            
            SetupMessage(Vault, ODX);
            
            vault.NotifyDistribution(distributionAmount);

            var certificates = vault.GetCertificates(Owner);
            certificates.Length.Should().Be(1);

            certificates[0].Should().BeEquivalentTo(expectedCertificate);
            
            VerifyLog(new VaultCertificateCreatedLog
            {
                Wallet = Owner, 
                Amount = distributionAmount, 
                VestedBlock = block + BlocksPerYear
            }, Times.Once);
        }
        
        [Fact]
        public void NotifyDistribution_ExistingCertificates_Success()
        {
            UInt256 distributionAmount = 50;
            const ulong block = 100;

            var existingCertificate = new VaultCertificate {Amount = 100, VestedBlock = 500};
            var expectedCertificate = new VaultCertificate {Amount = distributionAmount, VestedBlock = block + BlocksPerYear};
            
            var vault = CreateNewOpdexVault(block);
            
            PersistentState.SetArray($"Certificates:{Owner}", new[] { existingCertificate });
            
            SetupMessage(Vault, ODX);
            
            vault.NotifyDistribution(distributionAmount);

            var certificates = vault.GetCertificates(Owner);
            certificates.Length.Should().Be(2);

            certificates[0].Should().BeEquivalentTo(existingCertificate);
            certificates[1].Should().BeEquivalentTo(expectedCertificate);
            
            VerifyLog(new VaultCertificateCreatedLog
            {
                Wallet = Owner, 
                Amount = distributionAmount, 
                VestedBlock = block + BlocksPerYear
            }, Times.Once);
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
            PersistentState.SetAddress(nameof(IOpdexVault.Token), ODX);
            
            SetupMessage(Vault, Owner);
            
            vault.RedeemCertificates();

            vault.GetCertificates(Owner).Should().BeEquivalentTo(new VaultCertificate[0]);

            VerifyCall(ODX, 0, nameof(IOpdexMinedToken.TransferTo), It.IsAny<object[]>(), Times.Never);
            VerifyLog(It.IsAny<VaultCertificateRedeemedLog>(), Times.Never);
        }
        
        [Fact]
        public void RedeemCertificates_SingleValid_Success()
        {
            const ulong block = 1000;

            var existingCertificate = new VaultCertificate {Amount = 100, VestedBlock = 500};
            
            var vault = CreateNewOpdexVault(block);
            
            PersistentState.SetArray($"Certificates:{Owner}", new[] { existingCertificate });
            PersistentState.SetAddress(nameof(IOpdexVault.Token), ODX);
            
            SetupMessage(Vault, Owner);

            var transferToParams = new object[] {Owner, existingCertificate.Amount};
            SetupCall(ODX, 0, nameof(IOpdexMinedToken.TransferTo), transferToParams, TransferResult.Transferred(true));
            
            vault.RedeemCertificates();

            vault.GetCertificates(Owner).Length.Should().Be(0);

            VerifyCall(ODX, 0, nameof(IOpdexMinedToken.TransferTo), transferToParams, Times.Once);
            VerifyLog(new VaultCertificateRedeemedLog {Wallet = Owner, Amount = existingCertificate.Amount }, Times.Once);
        }
        
        [Fact]
        public void RedeemCertificates_SingleInvalid_Success()
        {
            const ulong block = 1000;

            var existingCertificate = new VaultCertificate {Amount = 100, VestedBlock = block + 1};
            
            var vault = CreateNewOpdexVault(block);
            
            PersistentState.SetArray($"Certificates:{Owner}", new[] { existingCertificate });
            PersistentState.SetAddress(nameof(IOpdexVault.Token), ODX);
            
            SetupMessage(Vault, Owner);
            
            vault.RedeemCertificates();

            vault.GetCertificates(Owner).Single().Should().BeEquivalentTo(existingCertificate);

            VerifyCall(ODX, 0, nameof(IOpdexMinedToken.TransferTo), It.IsAny<object[]>(), Times.Never);
            VerifyLog(It.IsAny<VaultCertificateRedeemedLog>(), Times.Never);
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
            PersistentState.SetAddress(nameof(IOpdexVault.Token), ODX);
            
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
            VerifyLog(new VaultCertificateRedeemedLog {Wallet = Owner, Amount = existingCertificates[0].Amount }, Times.Once);
            VerifyLog(new VaultCertificateRedeemedLog {Wallet = Owner, Amount = existingCertificates[1].Amount }, Times.Once);
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
            PersistentState.SetAddress(nameof(IOpdexVault.Token), ODX);
            
            SetupMessage(Vault, Owner);
            
            vault.RedeemCertificates();

            vault.GetCertificates(Owner).Should().BeEquivalentTo(existingCertificates);

            VerifyCall(ODX, 0, nameof(IOpdexMinedToken.TransferTo), It.IsAny<object[]>(), Times.Never);
            VerifyLog(It.IsAny<VaultCertificateRedeemedLog>(), Times.Never);
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
            PersistentState.SetAddress(nameof(IOpdexVault.Token), ODX);
            
            SetupMessage(Vault, Owner);

            var transferToParams = new object[] {Owner, expectedTotalTransfer};
            SetupCall(ODX, 0, nameof(IOpdexMinedToken.TransferTo), transferToParams, TransferResult.Transferred(true));
            
            vault.RedeemCertificates();

            var certificates = vault.GetCertificates(Owner);
            certificates.Length.Should().Be(0);

            VerifyCall(ODX, 0, nameof(IOpdexMinedToken.TransferTo), transferToParams, Times.Once);
            VerifyLog(new VaultCertificateRedeemedLog {Wallet = Owner, Amount = existingCertificates[0].Amount }, Times.Once);
            VerifyLog(new VaultCertificateRedeemedLog {Wallet = Owner, Amount = existingCertificates[1].Amount }, Times.Once);
        }

        [Fact]
        public void CreateCertificate_NewHolder_Success()
        {
            const ulong block = 2500;
            UInt256 transferAmount = 25;
            UInt256 expectedOpdexCertificateBalance = 75;
            
            var existingCertificates = new[] { new VaultCertificate {Amount = 100, VestedBlock = 3000} };
            
            var vault = CreateNewOpdexVault(block);
            
            PersistentState.SetArray($"Certificates:{Owner}", existingCertificates);
            PersistentState.SetAddress(nameof(IOpdexVault.Token), ODX);
            
            SetupMessage(Vault, Owner);
            
            vault.CreateCertificate(Miner1, transferAmount, BlocksPerYear).Should().BeTrue();

            var opdexCertificates = vault.GetCertificates(Owner);
            var minerCertificates = vault.GetCertificates(Miner1);

            opdexCertificates.Single().Amount.Should().Be(expectedOpdexCertificateBalance);
            minerCertificates.Single().Amount.Should().Be(transferAmount);
            minerCertificates.Single().VestedBlock.Should().Be(block + BlocksPerYear);
            
            VerifyLog(new VaultCertificateCreatedLog
            {
                Wallet = Miner1, 
                Amount = transferAmount, 
                VestedBlock = block + BlocksPerYear
            }, Times.Once);
        }

        [Fact]
        public void CreateCertificate_ExistingHolder_Success()
        {
            const ulong block = 2500;
            UInt256 transferAmount = 25;
            UInt256 expectedOpdexCertificateBalance = 75;
            
            var existingOwnerCertificates = new[] { new VaultCertificate {Amount = 100, VestedBlock = 3000} };
            var existingMinerCertificates = new[] { new VaultCertificate {Amount = 100, VestedBlock = 3000} };
            
            var vault = CreateNewOpdexVault(block);
            
            PersistentState.SetArray($"Certificates:{Owner}", existingOwnerCertificates);
            PersistentState.SetArray($"Certificates:{Miner1}", existingMinerCertificates);
            PersistentState.SetAddress(nameof(IOpdexVault.Token), ODX);
            
            SetupMessage(Vault, Owner);
            
            vault.CreateCertificate(Miner1, transferAmount, BlocksPerYear).Should().BeTrue();

            var opdexCertificates = vault.GetCertificates(Owner);
            var minerCertificates = vault.GetCertificates(Miner1);

            opdexCertificates.Single().Amount.Should().Be(expectedOpdexCertificateBalance);
            minerCertificates.Length.Should().Be(2);
            minerCertificates[0].Amount.Should().Be((UInt256)100);
            minerCertificates[0].VestedBlock.Should().Be(3000);
            minerCertificates[1].Amount.Should().Be(transferAmount);
            minerCertificates[1].VestedBlock.Should().Be(block + BlocksPerYear);
            
            VerifyLog(new VaultCertificateCreatedLog
            {
                Wallet = Miner1, 
                Amount = transferAmount, 
                VestedBlock = block + BlocksPerYear
            }, Times.Once);
        }
        
        [Fact]
        public void CreateCertificate_Fail_InsufficientAmount()
        {
            const ulong block = 2500;
            UInt256 transferAmount = 25;
            UInt256 expectedOpdexCertificateBalance = 10;
            
            var existingCertificates = new[] { new VaultCertificate {Amount = 10, VestedBlock = 3000} };
            
            var vault = CreateNewOpdexVault(block);
            
            PersistentState.SetArray($"Certificates:{Owner}", existingCertificates);
            PersistentState.SetAddress(nameof(IOpdexVault.Token), ODX);
            
            SetupMessage(Vault, Owner);
            
            vault.CreateCertificate(Miner1, transferAmount, BlocksPerYear).Should().BeFalse();

            var opdexCertificates = vault.GetCertificates(Owner);
            var minerCertificates = vault.GetCertificates(Miner1);

            opdexCertificates.Single().Amount.Should().Be(expectedOpdexCertificateBalance);
            minerCertificates.Should().BeEquivalentTo(new VaultCertificate[0]);
            
            VerifyLog(It.IsAny<VaultCertificateCreatedLog>(), Times.Never);
        }
        
        [Fact]
        public void CreateCertificate_Throws_Unauthorized()
        {
            const ulong block = 2500;
            UInt256 transferAmount = 25;
            
            var vault = CreateNewOpdexVault(block);
            
            SetupMessage(Vault, Miner1);
            
            vault
                .Invoking(v => v.CreateCertificate(Miner1, transferAmount, BlocksPerYear))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }
        
        [Fact]
        public void CreateCertificate_Throws_ZeroAmount()
        {
            const ulong block = 2500;
            UInt256 transferAmount = 0;
            
            var vault = CreateNewOpdexVault(block);
            
            SetupMessage(Vault, Owner);
            
            vault
                .Invoking(v => v.CreateCertificate(Miner1, transferAmount, BlocksPerYear))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: ZERO_AMOUNT");
        }
        
        [Fact]
        public void CreateCertificate_Throws_InsufficientVestingPeriod()
        {
            const ulong block = 2500;
            UInt256 transferAmount = 25;
            const ulong invalidVestingPeriod = 499;
            
            var existingCertificates = new[] { new VaultCertificate {Amount = 100, VestedBlock = 3000} };
            
            var vault = CreateNewOpdexVault(block);
            
            PersistentState.SetArray($"Certificates:{Owner}", existingCertificates);
            PersistentState.SetAddress(nameof(IOpdexVault.Token), ODX);

            SetupMessage(Vault, Owner);
            
            vault
                .Invoking(v => v.CreateCertificate(Miner1, transferAmount, invalidVestingPeriod))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_VESTING_PERIOD");
        }
        
        [Fact]
        public void SetOwner_Success()
        {
            var vault = CreateNewOpdexVault();
            
            SetupMessage(ODX, Owner);
            
            vault.SetOwner(MiningGovernance);

            vault.Owner.Should().Be(MiningGovernance);

            VerifyLog(new VaultOwnerChangeLog
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
    }
}