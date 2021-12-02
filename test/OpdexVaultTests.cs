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

            vault.Token.Should().Be(ODX);
            vault.PledgeMinimum.Should().Be(100ul);
            vault.ProposalMinimum.Should().Be(200ul);
            vault.VestingDuration.Should().Be(BlocksPerYear);
            vault.NextProposalId.Should().Be(1ul);
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

        #region Redeem Certificate

        [Fact]
        public void RedeemCertificate_Success()
        {
            const ulong block = 1000;
            const ulong vestedBlock = block - 1;

            var existingCertificate = new Certificate {Amount = 100, VestedBlock = vestedBlock};

            var vault = CreateNewOpdexVault(block);

            State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", existingCertificate);

            SetupMessage(Vault, Miner);

            var transferToParams = new object[] {Miner, existingCertificate.Amount};
            SetupCall(ODX, 0, nameof(IOpdexMinedToken.TransferTo), transferToParams, TransferResult.Transferred(true));

            vault.RedeemCertificate();

            vault.GetCertificate(Miner).Should().BeEquivalentTo(default(Certificate));

            VerifyCall(ODX, 0, nameof(IOpdexMinedToken.TransferTo), transferToParams, Times.Once);
            VerifyLog(new RedeemVaultCertificateLog {Owner = Miner, Amount = existingCertificate.Amount, VestedBlock = vestedBlock }, Times.Once);
        }

        [Fact]
        public void RedeemCertificate_Throws_NotPayable()
        {
            const ulong block = 1000;

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner, 10);

            vault
                .Invoking(v => v.RedeemCertificate())
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: NOT_PAYABLE");
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

            var existingCertificate = new Certificate {Amount = 100, VestedBlock = block + 1};

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

        #region Create Proposal

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData(null)]
        [InlineData("qo7wDjrnvRYqhiIY3YPGXNXssOUe1oPkaJUnZskaXtY0ONivVXYCabTFxGtNygGwMBWE548IBU7T8Kp2JRrZB15jQbB5wy9b6ZIxncqzcOMadP3Tup0Uq3BOucHy64uDOaNBxKFelLPN0clRZ3ghUg2d2mu6vwFobufDqUPAfRQxRqeVWKPPjHYMVmroAcIWZRqbl0Ubd")]
        public void CreateProposal_Throws_InvalidDescription(string description)
        {
            const ulong block = 1000;
            UInt256 amount = 100;
            var certificate = new Certificate { Amount = amount, VestedBlock = BlocksPerYear, Revoked = false };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", certificate);

            vault
                .Invoking(v => v.CreateNewCertificateProposal(amount, Miner, description))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_DESCRIPTION");

            vault
                .Invoking(v => v.CreateRevokeCertificateProposal(Miner, description))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_DESCRIPTION");

            vault
                .Invoking(v => v.CreatePledgeMinimumProposal(amount, description))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_DESCRIPTION");

            vault
                .Invoking(v => v.CreateProposalMinimumProposal(amount, description))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_DESCRIPTION");
        }

        [Fact]
        public void CreateProposal_Throws_ZeroAmount()
        {
            const ulong block = 1000;
            const string description = "create a certificate.";
            UInt256 amount = 0;

            var certificate = new Certificate { Amount = amount, VestedBlock = BlocksPerYear, Revoked = false };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", certificate);

            vault
                .Invoking(v => v.CreateNewCertificateProposal(amount, Miner, description))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_AMOUNT");

            vault
                .Invoking(v => v.CreateRevokeCertificateProposal(Miner, description))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_AMOUNT");

            vault
                .Invoking(v => v.CreatePledgeMinimumProposal(amount, description))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_AMOUNT");

            vault
                .Invoking(v => v.CreateProposalMinimumProposal(amount, description))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_AMOUNT");
        }

        [Fact]
        public void CreateProposal_Throws_NotPayable()
        {
            const ulong block = 1000;
            const string description = "create a certificate.";
            UInt256 amount = 0;

            var certificate = new Certificate { Amount = amount, VestedBlock = BlocksPerYear, Revoked = false };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner, 10);

            State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", certificate);

            vault
                .Invoking(v => v.CreateNewCertificateProposal(amount, Miner, description))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: NOT_PAYABLE");

            vault
                .Invoking(v => v.CreateRevokeCertificateProposal(Miner, description))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: NOT_PAYABLE");

            vault
                .Invoking(v => v.CreatePledgeMinimumProposal(amount, description))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: NOT_PAYABLE");

            vault
                .Invoking(v => v.CreateProposalMinimumProposal(amount, description))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: NOT_PAYABLE");
        }

        [Fact]
        public void NewCertificateProposal_Success()
        {
            const ulong block = 1000;
            const string description = "create a certificate.";
            UInt256 amount = 100;
            const byte type = (byte)ProposalType.Create;
            const byte status = (byte)ProposalStatus.Pledge;
            const ulong expectedProposalId = 1;
            var expectedProposal = new ProposalDetails
            {
                Amount = amount,
                Wallet = Miner,
                Type = type,
                Status = status,
                Expiration = block + OneWeek,
                YesAmount = 0,
                NoAmount = 0,
                PledgeAmount = 0
            };

            var vault = CreateNewOpdexVault(block);

            State.SetUInt256($"{VaultStateKeys.TotalSupply}", amount);

            SetupMessage(Vault, Miner);

            var proposalId = vault.CreateNewCertificateProposal(amount, Miner, description);

            proposalId.Should().Be(expectedProposalId);
            vault.NextProposalId.Should().Be(expectedProposalId + 1);
            vault.TotalProposedAmount.Should().Be(amount);
            vault.GetCertificateProposalIdByRecipient(Miner).Should().Be(expectedProposalId);
            vault.GetProposal(expectedProposalId).Should().BeEquivalentTo(expectedProposal);

            VerifyLog(new CreateVaultProposalLog
            {
                ProposalId = expectedProposalId,
                Wallet = Miner,
                Amount = amount,
                Description = description,
                Type = type,
                Status = status,
                Expiration = block + OneWeek
            }, Times.Once);
        }

        [Fact]
        public void NewCertificateProposal_Throws_CertificateExists()
        {
            const ulong block = 1000;
            const string description = "create a certificate.";
            UInt256 amount = 100;
            var certificate = new Certificate { Amount = amount, VestedBlock = BlocksPerYear + BlocksPerMonth };

            var vault = CreateNewOpdexVault(block);

            State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", certificate);

            vault
                .Invoking(v => v.CreateNewCertificateProposal(amount, Miner, description))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: CERTIFICATE_EXISTS");
        }

        [Fact]
        public void NewCertificateProposal_Throws_InvalidVaultSupply()
        {
            const ulong block = 1000;
            const string description = "create a certificate.";
            UInt256 vaultSupply = 200;
            UInt256 proposedAmount = 190;
            UInt256 amount = 100;

            var vault = CreateNewOpdexVault(block);

            State.SetUInt256($"{VaultStateKeys.TotalSupply}", vaultSupply);
            State.SetUInt256($"{VaultStateKeys.TotalProposedAmount}", proposedAmount);

            vault
                .Invoking(v => v.CreateNewCertificateProposal(amount, Miner, description))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_VAULT_SUPPLY");
        }

        [Fact]
        public void NewCertificateProposal_Throws_ProposalInProgress()
        {
            const ulong proposalId = 1;
            const ulong block = 1000;
            const string description = "create a certificate.";
            UInt256 vaultSupply = 1000;
            UInt256 proposedAmount = 190;
            UInt256 amount = 100;

            var vault = CreateNewOpdexVault(block);

            State.SetUInt256($"{VaultStateKeys.TotalSupply}", vaultSupply);
            State.SetUInt256($"{VaultStateKeys.TotalProposedAmount}", proposedAmount);
            State.SetUInt64($"{VaultStateKeys.ProposalIdByRecipient}:{Miner}", proposalId);

            vault
                .Invoking(v => v.CreateNewCertificateProposal(amount, Miner, description))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: RECIPIENT_PROPOSAL_IN_PROGRESS");
        }

        [Fact]
        public void RevokeCertificateProposal_Success()
        {
            const string description = "revoke a certificate.";
            UInt256 amount = 100;
            const byte type = (byte)ProposalType.Revoke;
            const byte status = (byte)ProposalStatus.Pledge;
            const ulong expectedProposalId = 1;
            var certificate = new Certificate { Amount = amount, VestedBlock = BlocksPerYear + BlocksPerMonth };
            var expectedProposal = new ProposalDetails
            {
                Amount = amount,
                Wallet = Miner,
                Type = type,
                Status = status,
                Expiration = BlocksPerYear + OneWeek,
                YesAmount = 0,
                NoAmount = 0,
                PledgeAmount = 0
            };

            var vault = CreateNewOpdexVault(BlocksPerYear);

            State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", certificate);

            SetupMessage(Vault, Miner);

            var proposalId = vault.CreateRevokeCertificateProposal(Miner, description);

            proposalId.Should().Be(expectedProposalId);
            vault.NextProposalId.Should().Be(expectedProposalId + 1);
            vault.GetCertificateProposalIdByRecipient(Miner).Should().Be(expectedProposalId);
            vault.GetProposal(expectedProposalId).Should().BeEquivalentTo(expectedProposal);

            VerifyLog(new CreateVaultProposalLog
            {
                ProposalId = expectedProposalId,
                Wallet = Miner,
                Amount = amount,
                Description = description,
                Type = type,
                Status = status,
                Expiration = BlocksPerYear + OneWeek
            }, Times.Once);
        }

        [Theory]
        [InlineData(true, BlocksPerYear, 100)]
        [InlineData(false, 100, 100)]
        public void RevokeCertificateProposal_Throws_InvalidCertificate(bool revoked, ulong vestedBlock, UInt256 amount)
        {
            const ulong block = 1000;
            const string description = "revoke a certificate.";
            var certificate = new Certificate { Amount = amount, VestedBlock = vestedBlock, Revoked = revoked};

            var vault = CreateNewOpdexVault(block);

            State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", certificate);

            vault
                .Invoking(v => v.CreateRevokeCertificateProposal(Miner, description))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_CERTIFICATE");
        }

        [Fact]
        public void RevokeCertificateProposal_Throws_ProposalInProgress()
        {
            const ulong proposalId = 1;
            const ulong block = 1000;
            const string description = "revoke a certificate.";
            var certificate = new Certificate { Amount = 100, VestedBlock = BlocksPerYear, Revoked = false};

            var vault = CreateNewOpdexVault(block);

            State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", certificate);
            State.SetUInt64($"{VaultStateKeys.ProposalIdByRecipient}:{Miner}", proposalId);

            vault
                .Invoking(v => v.CreateRevokeCertificateProposal(Miner, description))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: RECIPIENT_PROPOSAL_IN_PROGRESS");
        }

        [Fact]
        public void PledgeMinimumProposal_Success()
        {
            const ulong block = 1000;
            const string description = "change pledge minimum.";
            const byte type = (byte)ProposalType.PledgeMinimum;
            const byte status = (byte)ProposalStatus.Pledge;
            UInt256 amount = 100;
            const ulong expectedProposalId = 1;
            var expectedProposal = new ProposalDetails
            {
                Amount = amount,
                Wallet = Miner,
                Type = type,
                Status = status,
                Expiration = block + OneWeek,
                YesAmount = 0,
                NoAmount = 0,
                PledgeAmount = 0
            };
            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            var proposalId = vault.CreatePledgeMinimumProposal(amount, description);

            proposalId.Should().Be(expectedProposalId);
            vault.NextProposalId.Should().Be(expectedProposalId + 1);
            vault.GetCertificateProposalIdByRecipient(Miner).Should().Be(0ul);
            vault.GetProposal(expectedProposalId).Should().BeEquivalentTo(expectedProposal);

            VerifyLog(new CreateVaultProposalLog
            {
                ProposalId = expectedProposalId,
                Wallet = Miner,
                Amount = amount,
                Description = description,
                Type = type,
                Status = status,
                Expiration = block + OneWeek
            }, Times.Once);
        }

        [Fact]
        public void PledgeMinimumProposal_Throws_InvalidAmount()
        {
            const ulong block = 1000;
            const string description = "change pledge minimum.";
            UInt256 minimumAmountRequest = ((UInt256)ulong.MaxValue) + 1;

            var vault = CreateNewOpdexVault(block);

            vault
                .Invoking(v => v.CreatePledgeMinimumProposal(minimumAmountRequest, description))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXCESSIVE_AMOUNT");
        }

        [Fact]
        public void ProposalMinimumProposal_Success()
        {
            const ulong block = 1000;
            const string description = "change proposal minimum.";
            const byte type = (byte)ProposalType.ProposalMinimum;
            const byte status = (byte)ProposalStatus.Pledge;
            UInt256 amount = 100;
            const ulong expectedProposalId = 1;
            var expectedProposal = new ProposalDetails
            {
                Amount = amount,
                Wallet = Miner,
                Type = type,
                Status = status,
                Expiration = block + OneWeek,
                YesAmount = 0,
                NoAmount = 0,
                PledgeAmount = 0
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            var proposalId = vault.CreateProposalMinimumProposal(amount, description);

            proposalId.Should().Be(expectedProposalId);
            vault.NextProposalId.Should().Be(expectedProposalId + 1);
            vault.GetCertificateProposalIdByRecipient(Miner).Should().Be(0ul);
            vault.GetProposal(expectedProposalId).Should().BeEquivalentTo(expectedProposal);

            VerifyLog(new CreateVaultProposalLog
            {
                ProposalId = expectedProposalId,
                Wallet = Miner,
                Amount = amount,
                Description = description,
                Type = type,
                Status = status,
                Expiration = block + OneWeek
            }, Times.Once);
        }

        [Fact]
        public void ProposalMinimumProposal_Throws_InvalidAmount()
        {
            const ulong block = 1000;
            const string description = "change proposal minimum.";
            UInt256 minimumAmountRequest = ((UInt256)ulong.MaxValue) + 1;
            var vault = CreateNewOpdexVault(block);

            vault
                .Invoking(v => v.CreateProposalMinimumProposal(minimumAmountRequest, description))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXCESSIVE_AMOUNT");
        }

        #endregion

        #region Pledge

        [Theory]
        [InlineData(100, 10, 25, 0)]
        [InlineData(100, 100, 25, 50)]
        [InlineData(100, 10, 90, 75)]
        public void Pledge_Success(ulong minimumProposalPledgeAmount, ulong pledgeAmount, ulong currentProposalPledgeAmount, ulong currentWalletPledgeAmount)
        {
            const ulong proposalId = 1;
            const ulong block = 1000;
            var expectedWalletPledgeAmount = currentWalletPledgeAmount + pledgeAmount;
            var expectedProposalPledgeAmount = currentProposalPledgeAmount + pledgeAmount;

            var proposal = new ProposalDetails
            {
                Amount = pledgeAmount,
                Wallet = Miner,
                Type = (byte)ProposalType.Create,
                Status = (byte)ProposalStatus.Pledge,
                Expiration = block + BlocksPerYear,
                YesAmount = 0,
                NoAmount = 0,
                PledgeAmount = currentProposalPledgeAmount
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner, pledgeAmount);

            State.SetUInt64($"{VaultStateKeys.ProposalPledge}:{proposalId}:{Miner}", currentWalletPledgeAmount);
            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);
            State.SetUInt64(VaultStateKeys.PledgeMinimum, minimumProposalPledgeAmount);

            vault.Pledge(proposalId);

            var finalProposal = vault.GetProposal(proposalId);
            var minimumMet = expectedProposalPledgeAmount >= minimumProposalPledgeAmount;

            vault.GetProposalPledge(proposalId, Miner).Should().Be(expectedWalletPledgeAmount);
            finalProposal.PledgeAmount.Should().Be(expectedProposalPledgeAmount);
            finalProposal.Status.Should().Be(minimumMet ? (byte)ProposalStatus.Vote : proposal.Status);
            finalProposal.Expiration.Should().Be(minimumMet ? block + ThreeDays : proposal.Expiration);

            VerifyLog(new VaultProposalPledgeLog
            {
                ProposalId = proposalId,
                Pledger = Miner,
                PledgeAmount = pledgeAmount,
                PledgerAmount = expectedWalletPledgeAmount,
                ProposalPledgeAmount = expectedProposalPledgeAmount,
                PledgeMinimumMet = minimumMet
            }, Times.Once);
        }

        [Theory]
        [InlineData(ProposalStatus.Vote)]
        [InlineData(ProposalStatus.Complete)]
        public void Pledge_Throws_InvalidStatus(ProposalStatus status)
        {
            const ulong proposalId = 1;
            const ulong block = 1000;

            var proposal = new ProposalDetails
            {
                Amount = 100,
                Wallet = Miner,
                Type = (byte)ProposalType.Create,
                Status = (byte)status,
                Expiration = block + BlocksPerYear,
                YesAmount = 0,
                NoAmount = 0,
                PledgeAmount = 10
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner, 25);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);

            vault
                .Invoking(v => v.Pledge(proposalId))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_STATUS");
        }

        [Fact]
        public void Pledge_Throws_ProposalExpired()
        {
            const ulong proposalId = 1;
            const ulong block = 1000;

            var proposal = new ProposalDetails
            {
                Amount = 100,
                Wallet = Miner,
                Type = (byte)ProposalType.Create,
                Status = (byte)ProposalStatus.Pledge,
                Expiration = block - 1,
                YesAmount = 0,
                NoAmount = 0,
                PledgeAmount = 10
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner, 25);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);

            vault
                .Invoking(v => v.Pledge(proposalId))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: PROPOSAL_EXPIRED");
        }

        [Fact]
        public void Pledge_Throws_InsufficientPledgeAmount()
        {
            const ulong proposalId = 1;
            const ulong block = 1000;

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            vault
                .Invoking(v => v.Pledge(proposalId))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_PLEDGE_AMOUNT");
        }

        [Fact]
        public void WithdrawPledge_Throws_InsufficientWithdrawAmount()
        {
            const ulong proposalId = 1;
            const ulong block = 1000;

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            vault
                .Invoking(v => v.WithdrawPledge(proposalId, 0ul))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_WITHDRAW_AMOUNT");
        }

        #endregion

        #region Withdraw Pledge

        [Theory]
        [InlineData(100, 10, 100, 99, ProposalStatus.Pledge)]
        [InlineData(100, 100, 100, 100, ProposalStatus.Pledge)]
        [InlineData(100, 50, 100, 99, ProposalStatus.Vote)]
        [InlineData(100, 100, 100, 100, ProposalStatus.Vote)]
        [InlineData(100, 50, 100, 99, ProposalStatus.Complete)]
        [InlineData(100, 100, 100, 100, ProposalStatus.Complete)]
        public void WithdrawPledge_Success(ulong currentWalletPledgeAmount, ulong withdrawAmount, ulong currentBlock,
                                           ulong pledgeExpiration, ProposalStatus status)
        {
            const ulong proposalId = 1;
            var expectedWalletPledgeAmount = currentWalletPledgeAmount - withdrawAmount;

            var proposal = new ProposalDetails
            {
                Amount = 100,
                Wallet = Miner,
                Type = (byte)ProposalType.PledgeMinimum,
                Status = (byte)status,
                Expiration = pledgeExpiration,
                YesAmount = 0,
                NoAmount = 0,
                PledgeAmount = currentWalletPledgeAmount + 100
            };

            var vault = CreateNewOpdexVault(currentBlock);

            SetupMessage(Vault, Miner);
            SetupTransfer(Miner, withdrawAmount, TransferResult.Transferred(true));

            State.SetUInt64($"{VaultStateKeys.ProposalPledge}:{proposalId}:{Miner}", currentWalletPledgeAmount);
            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);

            vault.WithdrawPledge(proposalId, withdrawAmount);

            var finalProposal = vault.GetProposal(proposalId);

            var proposalIsActive = pledgeExpiration >= currentBlock;
            var voteWithdrawn = status == ProposalStatus.Pledge && proposalIsActive;
            var finalProposalPledgeAmount = voteWithdrawn
                ? proposal.PledgeAmount - withdrawAmount
                : proposal.PledgeAmount;

            finalProposal.PledgeAmount.Should().Be(finalProposalPledgeAmount);

            vault.GetProposalPledge(proposalId, Miner).Should().Be(expectedWalletPledgeAmount);

            VerifyTransfer(Miner, withdrawAmount, Times.Once);
            VerifyLog(new VaultProposalWithdrawPledgeLog
            {
                ProposalId = proposalId,
                Pledger = Miner,
                WithdrawAmount = withdrawAmount,
                PledgerAmount = expectedWalletPledgeAmount,
                ProposalPledgeAmount = finalProposal.PledgeAmount,
                PledgeWithdrawn = voteWithdrawn
            }, Times.Once);

            if (!proposalIsActive && status != ProposalStatus.Complete)
            {
                VerifyLog(new CompleteVaultProposalLog { ProposalId = proposalId, Approved = false }, Times.Once);
                vault.PledgeMinimum.Should().Be((ulong)proposal.Amount);
            }
            else
            {
                VerifyLog(It.IsAny<CompleteVaultProposalLog>(), Times.Never);
            }
        }

        [Fact]
        public void WithdrawPledge_Throws_InsufficientFunds()
        {
            const ulong proposalId = 1;
            const ulong block = 1000;
            const ulong currentWalletPledgeAmount = 20;
            const ulong requestedWithdrawAmount = currentWalletPledgeAmount + 1;

            var proposal = new ProposalDetails
            {
                Amount = 100,
                Wallet = Miner,
                Type = (byte)ProposalType.Create,
                Status = (byte)ProposalStatus.Pledge,
                Expiration = 100,
                YesAmount = 0,
                NoAmount = 0,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            State.SetUInt64($"{VaultStateKeys.ProposalPledge}:{proposalId}:{Miner}", currentWalletPledgeAmount);
            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);

            vault
                .Invoking(v => v.WithdrawPledge(proposalId, requestedWithdrawAmount))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_FUNDS");
        }

        [Fact]
        public void WithdrawPledge_Throws_NotPayable()
        {
            const ulong proposalId = 1;
            const ulong block = 1000;

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner, 10);

            vault
                .Invoking(v => v.WithdrawPledge(proposalId, 100))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: NOT_PAYABLE");
        }

        #endregion

        #region Vote

        [Theory]
        [InlineData(true, 0, 100)]
        [InlineData(true, 200, 100)]
        [InlineData(false, 0, 300)]
        [InlineData(false, 200, 300)]
        public void Vote_Success(bool inFavor, ulong existingVoteAmount, ulong voteAmount)
        {
            const ulong proposalId = 1;
            const ulong block = 1000;
            const ulong yesAmount = 199;
            const ulong noAmount = 49;
            var totalVoterAmount = existingVoteAmount + voteAmount;

            var proposal = new ProposalDetails
            {
                Amount = 25,
                Wallet = Miner,
                Type = (byte)ProposalType.Create,
                Status = (byte)ProposalStatus.Vote,
                Expiration = block + ThreeDays,
                YesAmount = yesAmount,
                NoAmount = noAmount,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            var existingVote = existingVoteAmount > 0
                ? new ProposalVote { InFavor = inFavor, Amount = existingVoteAmount }
                : default;

            State.SetStruct($"{VaultStateKeys.ProposalVote}:{proposalId}:{Miner}", existingVote);
            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);

            SetupMessage(Vault, Miner, voteAmount);

            vault.Vote(proposalId, inFavor);

            var finalProposal = vault.GetProposal(proposalId);
            var finalProposalVote = vault.GetProposalVote(proposalId, Miner);

            finalProposal.YesAmount.Should().Be(inFavor ? proposal.YesAmount + voteAmount : proposal.YesAmount);
            finalProposal.NoAmount.Should().Be(!inFavor ? proposal.NoAmount + voteAmount : proposal.NoAmount);
            finalProposalVote.Amount.Should().Be(totalVoterAmount);
            finalProposalVote.InFavor.Should().Be(inFavor);

            VerifyLog(new VaultProposalVoteLog
            {
                ProposalId = proposalId,
                Voter = Miner,
                InFavor = inFavor,
                VoteAmount = voteAmount,
                VoterAmount = totalVoterAmount,
                ProposalYesAmount = finalProposal.YesAmount,
                ProposalNoAmount = finalProposal.NoAmount
            }, Times.Once);
        }

        [Theory]
        [InlineData(ProposalStatus.Pledge)]
        [InlineData(ProposalStatus.Complete)]
        [InlineData((ProposalStatus)100)]
        public void Vote_Throws_InvalidStatus(ProposalStatus status)
        {
            const ulong proposalId = 1;
            const ulong block = 1000;

            var proposal = new ProposalDetails
            {
                Amount = 25,
                Wallet = Miner,
                Type = (byte)ProposalType.Create,
                Status = (byte)status,
                Expiration = block + ThreeDays,
                YesAmount = 199,
                NoAmount = 49,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);

            SetupMessage(Vault, Miner, 100);

            vault
                .Invoking(v => v.Vote(proposalId, true))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_STATUS");
        }

        [Fact]
        public void Vote_Throws_InsufficientVoteAmount()
        {
            const ulong proposalId = 1;
            const ulong block = 1000;

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            vault
                .Invoking(v => v.Vote(proposalId, true))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_VOTE_AMOUNT");
        }

        [Fact]
        public void Vote_Throws_ProposalExpired()
        {
            const ulong proposalId = 1;
            const ulong block = 1000;

            var proposal = new ProposalDetails
            {
                Amount = 25,
                Wallet = Miner,
                Type = (byte)ProposalType.Create,
                Status = (byte)ProposalStatus.Vote,
                Expiration = block - 1,
                YesAmount = 199,
                NoAmount = 49,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);

            SetupMessage(Vault, Miner, 100);

            vault
                .Invoking(v => v.Vote(proposalId, true))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: PROPOSAL_EXPIRED");
        }

        [Fact]
        public void Vote_Throws_VotedNotInFavor()
        {
            const ulong proposalId = 1;
            const ulong block = 1000;
            const bool inFavorAttempt = true;
            const bool previousVoteInFavor = false;
            var existingVote = new ProposalVote { InFavor = previousVoteInFavor, Amount = 100 };
            var proposal = new ProposalDetails
            {
                Amount = 25,
                Wallet = Miner,
                Type = (byte)ProposalType.Create,
                Status = (byte)ProposalStatus.Vote,
                Expiration = block,
                YesAmount = 199,
                NoAmount = 49,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);
            State.SetStruct($"{VaultStateKeys.ProposalVote}:{proposalId}:{Miner}", existingVote);

            SetupMessage(Vault, Miner, 100);

            vault
                .Invoking(v => v.Vote(proposalId, inFavorAttempt))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: ALREADY_VOTED_NOT_IN_FAVOR");
        }

        [Fact]
        public void Vote_Throws_VotedInFavor()
        {
            const ulong proposalId = 1;
            const ulong block = 1000;
            const bool inFavorAttempt = false;
            const bool previousVoteInFavor = true;
            var existingVote = new ProposalVote { InFavor = previousVoteInFavor, Amount = 100 };
            var proposal = new ProposalDetails
            {
                Amount = 25,
                Wallet = Miner,
                Type = (byte)ProposalType.Create,
                Status = (byte)ProposalStatus.Vote,
                Expiration = block,
                YesAmount = 199,
                NoAmount = 49,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);
            State.SetStruct($"{VaultStateKeys.ProposalVote}:{proposalId}:{Miner}", existingVote);

            SetupMessage(Vault, Miner, 100);

            vault
                .Invoking(v => v.Vote(proposalId, inFavorAttempt))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: ALREADY_VOTED_IN_FAVOR");
        }

        #endregion

        #region Withdraw Vote

        [Theory]
        [InlineData(true, 100, 10, 100, 99, ProposalStatus.Pledge)]
        [InlineData(false, 100, 100, 100, 100, ProposalStatus.Pledge)]
        [InlineData(true, 100, 50, 100, 99, ProposalStatus.Vote)]
        [InlineData(false, 100, 50, 100, 99, ProposalStatus.Vote)]
        [InlineData(true, 100, 100, 100, 100, ProposalStatus.Vote)]
        [InlineData(false, 100, 100, 100, 100, ProposalStatus.Vote)]
        [InlineData(true, 100, 50, 100, 99, ProposalStatus.Complete)]
        [InlineData(false, 100, 100, 100, 100, ProposalStatus.Complete)]
        public void WithdrawVote_Success(bool inFavor, ulong currentWalletVoteAmount, ulong withdrawAmount, ulong currentBlock,
                                         ulong voteExpiration, ProposalStatus status)
        {
            const ulong proposalId = 1;
            var expectedWalletVoteAmount = currentWalletVoteAmount - withdrawAmount;

            var existingVote = new ProposalVote { InFavor = inFavor, Amount = currentWalletVoteAmount };
            const ulong currentYesAmount = 500;
            const ulong currentNoAmount = 1000;

            var proposal = new ProposalDetails
            {
                Amount = 100,
                Wallet = Miner,
                Type = (byte)ProposalType.ProposalMinimum,
                Status = (byte)status,
                Expiration = voteExpiration,
                YesAmount = currentYesAmount,
                NoAmount = currentNoAmount,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(currentBlock);

            SetupMessage(Vault, Miner);
            SetupTransfer(Miner, withdrawAmount, TransferResult.Transferred(true));

            State.SetStruct($"{VaultStateKeys.ProposalVote}:{proposalId}:{Miner}", existingVote);
            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);

            vault.WithdrawVote(proposalId, withdrawAmount);

            var finalProposal = vault.GetProposal(proposalId);

            ulong finalYesAmount = currentYesAmount;
            ulong finalNoAmount = currentNoAmount;

            var proposalIsActive = voteExpiration >= currentBlock;
            var voteWithdrawn = status == ProposalStatus.Vote && proposalIsActive;

            if (voteWithdrawn)
            {
                finalYesAmount = inFavor ? currentYesAmount - withdrawAmount : currentYesAmount;
                finalNoAmount = !inFavor ? currentNoAmount - withdrawAmount : currentNoAmount;
            }

            finalProposal.YesAmount.Should().Be(finalYesAmount);
            finalProposal.NoAmount.Should().Be(finalNoAmount);

            vault.GetProposalVote(proposalId, Miner).Amount.Should().Be(expectedWalletVoteAmount);

            VerifyTransfer(Miner, withdrawAmount, Times.Once);
            VerifyLog(new VaultProposalWithdrawVoteLog
            {
                ProposalId = proposalId,
                Voter = Miner,
                WithdrawAmount = withdrawAmount,
                VoterAmount = expectedWalletVoteAmount,
                ProposalYesAmount = finalProposal.YesAmount,
                ProposalNoAmount = finalProposal.NoAmount,
                VoteWithdrawn = voteWithdrawn
            }, Times.Once);

            if (!proposalIsActive && status != ProposalStatus.Complete)
            {
                VerifyLog(new CompleteVaultProposalLog { ProposalId = proposalId, Approved = false }, Times.Once);
                vault.PledgeMinimum.Should().Be((ulong)proposal.Amount);
            }
            else
            {
                VerifyLog(It.IsAny<CompleteVaultProposalLog>(), Times.Never);
            }
        }

        [Fact]
        public void WithdrawVote_Throws_InsufficientFunds()
        {
            const ulong proposalId = 1;
            const ulong block = 1000;
            const ulong currentWalletVoteAmount = 20;
            const ulong requestedWithdrawAmount = currentWalletVoteAmount + 1;

            var existingVote = new ProposalVote { InFavor = true, Amount = currentWalletVoteAmount };
            var proposal = new ProposalDetails
            {
                Amount = 100,
                Wallet = Miner,
                Type = (byte)ProposalType.Create,
                Status = (byte)ProposalStatus.Vote,
                Expiration = 100,
                YesAmount = 25,
                NoAmount = 70,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            State.SetStruct($"{VaultStateKeys.ProposalVote}:{proposalId}:{Miner}", existingVote);
            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);

            vault
                .Invoking(v => v.WithdrawVote(proposalId, requestedWithdrawAmount))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_FUNDS");
        }

        [Fact]
        public void WithdrawVote_Throws_NotPayable()
        {
            const ulong proposalId = 1;
            const ulong block = 1000;

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner, 10);

            vault
                .Invoking(v => v.WithdrawVote(proposalId, 100))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: NOT_PAYABLE");
        }

        [Fact]
        public void WithdrawVote_Throws_InsufficientWithdrawAmount()
        {
            const ulong proposalId = 1;
            const ulong block = 1000;

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            vault
                .Invoking(v => v.WithdrawVote(proposalId, 0ul))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_WITHDRAW_AMOUNT");
        }

        #endregion

        #region Complete Proposal

        [Fact]
        public void CompleteProposal_Throws_NotPayable()
        {
            const ulong proposalId = 1;
            const ulong block = 1000;

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner, 10);

            vault
                .Invoking(v => v.CompleteProposal(proposalId))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: NOT_PAYABLE");
        }

        [Fact]
        public void CompleteProposal_Throws_InvalidProposal()
        {
            const ulong proposalId = 1;
            const ulong block = 1000;

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            vault
                .Invoking(v => v.CompleteProposal(proposalId))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_PROPOSAL");
        }

        [Theory]
        [InlineData(ProposalStatus.Complete)]
        [InlineData((ProposalStatus)100)]
        public void CompleteProposal_Throws_InvalidStatus(ProposalStatus status)
        {
            const ulong proposalId = 1;
            const ulong block = 1000;
            var proposal = new ProposalDetails
            {
                Amount = 25,
                Wallet = Miner,
                Type = (byte)ProposalType.Create,
                Status = (byte)status,
                Expiration = block - 10,
                YesAmount = 199,
                NoAmount = 1,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);

            vault
                .Invoking(v => v.CompleteProposal(proposalId))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: ALREADY_COMPLETE");
        }

        [Theory]
        [InlineData(ProposalStatus.Pledge)]
        [InlineData(ProposalStatus.Vote)]
        public void CompleteProposal_Throws_ProposalInProgress(ProposalStatus status)
        {
            const ulong proposalId = 1;
            const ulong block = 1000;
            var proposal = new ProposalDetails
            {
                Amount = 25,
                Wallet = Miner,
                Type = (byte)ProposalType.Create,
                Status = (byte)status,
                Expiration = block,
                YesAmount = 199,
                NoAmount = 1,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);

            vault
                .Invoking(v => v.CompleteProposal(proposalId))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: PROPOSAL_IN_PROGRESS");
        }

        [Fact]
        public void CompleteProposal_NewCertificate_Approved_Success()
        {
            const ulong proposalId = 1;
            const ulong block = 1000;
            const ulong expectedVestedBlock = block + BlocksPerYear;
            UInt256 currentTotalSupply = 100;
            UInt256 expectedTotalSupply = 75;
            UInt256 currentProposedAmount = 75;
            UInt256 expectedProposedAmount = 50;
            UInt256 amount = 25;

            var proposal = new ProposalDetails
            {
                Amount = amount,
                Wallet = Miner,
                Type = (byte)ProposalType.Create,
                Status = (byte)ProposalStatus.Vote,
                Expiration = block - 1,
                YesAmount = 199,
                NoAmount = 1,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);
            State.SetUInt256(VaultStateKeys.TotalSupply, currentTotalSupply);
            State.SetUInt256(VaultStateKeys.TotalProposedAmount, currentProposedAmount);
            State.SetUInt64($"{VaultStateKeys.ProposalIdByRecipient}:{Miner}", proposalId);

            vault.CompleteProposal(proposalId);

            var minerCertificate = vault.GetCertificate(Miner);
            minerCertificate.Amount.Should().Be(amount);
            minerCertificate.VestedBlock.Should().Be(expectedVestedBlock);
            minerCertificate.Revoked.Should().BeFalse();

            var proposalResult = vault.GetProposal(proposalId);
            proposalResult.Status.Should().Be((byte)ProposalStatus.Complete);

            vault.TotalProposedAmount.Should().Be(expectedProposedAmount);
            vault.TotalSupply.Should().Be(expectedTotalSupply);
            vault.GetCertificateProposalIdByRecipient(Miner).Should().Be(0ul);

            VerifyLog(new CreateVaultCertificateLog { Owner = Miner, Amount = amount, VestedBlock = expectedVestedBlock }, Times.Once);
            VerifyLog(new CompleteVaultProposalLog { ProposalId = proposalId, Approved = true }, Times.Once);
        }

        [Theory]
        [InlineData(ProposalStatus.Pledge)]
        [InlineData(ProposalStatus.Vote)]
        public void CompleteProposal_NewCertificate_NotApproved_Success(ProposalStatus status)
        {
            const ulong proposalId = 1;
            const ulong block = 1000;
            UInt256 currentTotalSupply = 100;
            UInt256 expectedTotalSupply = 100;
            UInt256 currentProposedAmount = 75;
            UInt256 expectedProposedAmount = 50;
            UInt256 amount = 25;

            var proposal = new ProposalDetails
            {
                Amount = amount,
                Wallet = Miner,
                Type = (byte)ProposalType.Create,
                Status = (byte)status,
                Expiration = block - 1,
                YesAmount = 1,
                NoAmount = 99,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);
            State.SetUInt256(VaultStateKeys.TotalSupply, currentTotalSupply);
            State.SetUInt256(VaultStateKeys.TotalProposedAmount, currentProposedAmount);
            State.SetUInt64($"{VaultStateKeys.ProposalIdByRecipient}:{Miner}", proposalId);

            vault.CompleteProposal(proposalId);

            var minerCertificate = vault.GetCertificate(Miner);
            minerCertificate.Should().BeEquivalentTo(default(Certificate));

            var proposalResult = vault.GetProposal(proposalId);
            proposalResult.Status.Should().Be((byte)ProposalStatus.Complete);

            vault.TotalProposedAmount.Should().Be(expectedProposedAmount);
            vault.TotalSupply.Should().Be(expectedTotalSupply);
            vault.GetCertificateProposalIdByRecipient(Miner).Should().Be(0ul);

            VerifyLog(It.IsAny<CreateVaultCertificateLog>(), Times.Never);
            VerifyLog(new CompleteVaultProposalLog { ProposalId = proposalId, Approved = false }, Times.Once);
        }

        [Theory]
        [InlineData((ulong)(BlocksPerYear * .01m), 100, 1)] // vested 1% of the 4 years
        [InlineData((ulong)(BlocksPerYear * .25m), 100, 25)] // vested 25% of the 4 years
        [InlineData((ulong)(BlocksPerYear * .5m), 100, 50)] // vested 50% of the 4 years
        [InlineData((ulong)(BlocksPerYear * .75m), 100, 75)] // vested 75% of the 4 years
        [InlineData((ulong)(BlocksPerYear * .99m), 100, 99)] // vested 99% of the 4 years
        public void CompleteProposal_RevokeCertificate_Approved_Success(ulong block, UInt256 currentAmount, UInt256 expectedAmount)
        {
            const ulong proposalId = 1;
            UInt256 currentTotalSupply = 75;
            UInt256 expectedTotalSupply = currentTotalSupply + (currentAmount - expectedAmount);
            var existingMinerCertificate = new Certificate {Amount = currentAmount, VestedBlock = BlocksPerYear};

            var proposal = new ProposalDetails
            {
                Amount = currentAmount,
                Wallet = Miner,
                Type = (byte)ProposalType.Revoke,
                Status = (byte)ProposalStatus.Vote,
                Expiration = block - 1,
                YesAmount = 1320,
                NoAmount = 99,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);
            State.SetUInt256(VaultStateKeys.TotalSupply, currentTotalSupply);
            State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", existingMinerCertificate);
            State.SetUInt64($"{VaultStateKeys.ProposalIdByRecipient}:{Miner}", proposalId);

            vault.CompleteProposal(proposalId);

            var minerCertificate = vault.GetCertificate(Miner);
            minerCertificate.Amount.Should().Be(expectedAmount);
            minerCertificate.VestedBlock.Should().Be(BlocksPerYear);
            minerCertificate.Revoked.Should().BeTrue();

            var proposalResult = vault.GetProposal(proposalId);
            proposalResult.Status.Should().Be((byte)ProposalStatus.Complete);

            vault.TotalSupply.Should().Be(expectedTotalSupply);
            vault.GetCertificateProposalIdByRecipient(Miner).Should().Be(0ul);

            VerifyLog(new RevokeVaultCertificateLog { Owner = Miner, OldAmount = currentAmount, NewAmount = expectedAmount, VestedBlock = BlocksPerYear }, Times.Once);
            VerifyLog(new CompleteVaultProposalLog { ProposalId = proposalId, Approved = true }, Times.Once);
        }

        [Theory]
        [InlineData(ProposalStatus.Pledge)]
        [InlineData(ProposalStatus.Vote)]
        public void CompleteProposal_RevokeCertificate_NotApproved_Success(ProposalStatus status)
        {
            const ulong proposalId = 1;
            const ulong block = 100;
            UInt256 currentTotalSupply = 75;
            UInt256 expectedTotalSupply = 75;
            UInt256 amount = 100;
            var existingMinerCertificate = new Certificate {Amount = amount, VestedBlock = BlocksPerYear};

            var proposal = new ProposalDetails
            {
                Amount = amount,
                Wallet = Miner,
                Type = (byte)ProposalType.Revoke,
                Status = (byte)status,
                Expiration = block - 1,
                YesAmount = 1,
                NoAmount = 99,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);
            State.SetUInt256(VaultStateKeys.TotalSupply, currentTotalSupply);
            State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", existingMinerCertificate);
            State.SetUInt64($"{VaultStateKeys.ProposalIdByRecipient}:{Miner}", proposalId);

            vault.CompleteProposal(proposalId);

            var minerCertificate = vault.GetCertificate(Miner);
            minerCertificate.Amount.Should().Be(amount);
            minerCertificate.VestedBlock.Should().Be(BlocksPerYear);
            minerCertificate.Revoked.Should().BeFalse();

            var proposalResult = vault.GetProposal(proposalId);
            proposalResult.Status.Should().Be((byte)ProposalStatus.Complete);

            vault.TotalSupply.Should().Be(expectedTotalSupply);
            vault.GetCertificateProposalIdByRecipient(Miner).Should().Be(0ul);

            VerifyLog(It.IsAny<RevokeVaultCertificateLog>(), Times.Never);
            VerifyLog(new CompleteVaultProposalLog { ProposalId = proposalId, Approved = false }, Times.Once);
        }

        [Fact]
        public void CompleteProposal_RevokeCertificate_Continues_CertificateVested()
        {
            const ulong proposalId = 1;
            const ulong block = 100 + BlocksPerYear;
            UInt256 currentTotalSupply = 75;
            UInt256 amount = 100;
            var existingMinerCertificate = new Certificate {Amount = amount, VestedBlock = BlocksPerYear};

            var proposal = new ProposalDetails
            {
                Amount = amount,
                Wallet = Miner,
                Type = (byte)ProposalType.Revoke,
                Status = (byte)ProposalStatus.Vote,
                Expiration = 100,
                YesAmount = 101,
                NoAmount = 99,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);
            State.SetUInt256(VaultStateKeys.TotalSupply, currentTotalSupply);
            State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", existingMinerCertificate);
            State.SetUInt64($"{VaultStateKeys.ProposalIdByRecipient}:{Miner}", proposalId);

            vault.CompleteProposal(proposalId);

            var minerCertificate = vault.GetCertificate(Miner);
            minerCertificate.Amount.Should().Be(amount);
            minerCertificate.VestedBlock.Should().Be(BlocksPerYear);
            minerCertificate.Revoked.Should().BeFalse();

            var proposalResult = vault.GetProposal(proposalId);
            proposalResult.Status.Should().Be((byte)ProposalStatus.Complete);

            vault.TotalSupply.Should().Be(currentTotalSupply);
            vault.GetCertificateProposalIdByRecipient(Miner).Should().Be(0ul);

            VerifyLog(It.IsAny<RevokeVaultCertificateLog>(), Times.Never);
            VerifyLog(new CompleteVaultProposalLog { ProposalId = proposalId, Approved = true }, Times.Once);
        }

        [Fact]
        public void CompleteProposal_PledgeMinimum_Approved_Success()
        {
            const ulong proposalId = 1;
            const ulong block = 100;
            UInt256 requestedPledgeMinimum = 50;
            const ulong currentPledgeMinimum = 100;

            var proposal = new ProposalDetails
            {
                Amount = requestedPledgeMinimum,
                Wallet = Miner,
                Type = (byte)ProposalType.PledgeMinimum,
                Status = (byte)ProposalStatus.Vote,
                Expiration = block - 1,
                YesAmount = 103,
                NoAmount = 99,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);
            State.SetUInt64(VaultStateKeys.PledgeMinimum, currentPledgeMinimum);

            vault.CompleteProposal(proposalId);

            var proposalResult = vault.GetProposal(proposalId);
            proposalResult.Status.Should().Be((byte)ProposalStatus.Complete);

            vault.PledgeMinimum.Should().Be((ulong)requestedPledgeMinimum);

            VerifyLog(new CompleteVaultProposalLog { ProposalId = proposalId, Approved = true }, Times.Once);
        }

        [Theory]
        [InlineData(ProposalStatus.Pledge)]
        [InlineData(ProposalStatus.Vote)]
        public void CompleteProposal_PledgeMinimum_NotApproved_Success(ProposalStatus status)
        {
            const ulong proposalId = 1;
            const ulong block = 100;
            UInt256 requestedPledgeMinimum = 50;
            const ulong pledgeMinimum = 100;

            var proposal = new ProposalDetails
            {
                Amount = requestedPledgeMinimum,
                Wallet = Miner,
                Type = (byte)ProposalType.PledgeMinimum,
                Status = (byte)status,
                Expiration = block - 1,
                YesAmount = 1,
                NoAmount = 99,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);
            State.SetUInt64(VaultStateKeys.PledgeMinimum, pledgeMinimum);

            vault.CompleteProposal(proposalId);

            var proposalResult = vault.GetProposal(proposalId);
            proposalResult.Status.Should().Be((byte)ProposalStatus.Complete);

            vault.PledgeMinimum.Should().Be(pledgeMinimum);

            VerifyLog(new CompleteVaultProposalLog { ProposalId = proposalId, Approved = false }, Times.Once);
        }

        [Fact]
        public void CompleteProposal_ProposalMinimum_Approved_Success()
        {
            const ulong proposalId = 1;
            const ulong block = 100;
            const ulong minimumProposalAmount = 100;
            UInt256 requestedProposalMinimum = 50;

            var proposal = new ProposalDetails
            {
                Amount = requestedProposalMinimum,
                Wallet = Miner,
                Type = (byte)ProposalType.ProposalMinimum,
                Status = (byte)ProposalStatus.Vote,
                Expiration = block - 1,
                YesAmount = 100,
                NoAmount = 0,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);
            State.SetUInt64(VaultStateKeys.ProposalMinimum, minimumProposalAmount);

            vault.CompleteProposal(proposalId);

            var proposalResult = vault.GetProposal(proposalId);
            proposalResult.Status.Should().Be((byte)ProposalStatus.Complete);

            vault.ProposalMinimum.Should().Be((ulong)requestedProposalMinimum);

            VerifyLog(new CompleteVaultProposalLog { ProposalId = proposalId, Approved = true }, Times.Once);
        }

        [Theory]
        [InlineData(ProposalStatus.Pledge)]
        [InlineData(ProposalStatus.Vote)]
        public void CompleteProposal_ProposalMinimum_NotApproved_Success(ProposalStatus status)
        {
            const ulong proposalId = 1;
            const ulong block = 100;
            const ulong minimumProposalAmount = 100;
            UInt256 requestedProposalMinimum = 50;

            var proposal = new ProposalDetails
            {
                Amount = requestedProposalMinimum,
                Wallet = Miner,
                Type = (byte)ProposalType.ProposalMinimum,
                Status = (byte)status,
                Expiration = block - 1,
                YesAmount = 1,
                NoAmount = 99,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);
            State.SetUInt64(VaultStateKeys.ProposalMinimum, minimumProposalAmount);

            vault.CompleteProposal(proposalId);

            var proposalResult = vault.GetProposal(proposalId);
            proposalResult.Status.Should().Be((byte)ProposalStatus.Complete);

            vault.ProposalMinimum.Should().Be(minimumProposalAmount);

            VerifyLog(new CompleteVaultProposalLog { ProposalId = proposalId, Approved = false }, Times.Once);
        }

        #endregion
    }
}
