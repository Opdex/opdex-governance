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
            vault.NextProposalId.Should().Be((UInt256)1);
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
            const VaultProposalType type = VaultProposalType.Create;

            var vault = CreateNewOpdexVault(block);

            vault
                .Invoking(v => v.CreateProposal(amount, Miner, description, (byte)type))
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
            const VaultProposalType type = VaultProposalType.Create;

            var vault = CreateNewOpdexVault(block);

            vault
                .Invoking(v => v.CreateProposal(amount, Miner, description, (byte)type))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_AMOUNT");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        public void CreateProposal_Throws_InvalidType(byte value)
        {
            const ulong block = 1000;
            const string description = "create a certificate.";
            UInt256 amount = 100;
            var type = (VaultProposalType)value;

            var vault = CreateNewOpdexVault(block);

            vault
                .Invoking(v => v.CreateProposal(amount, Miner, description, (byte)type))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_PROPOSAL_TYPE");
        }

        [Fact]
        public void CreateProposal_NewCertificate_Success()
        {
            const ulong block = 1000;
            const string description = "create a certificate.";
            UInt256 amount = 100;
            const VaultProposalType type = VaultProposalType.Create;

            var vault = CreateNewOpdexVault(block);

            State.SetUInt256($"{VaultStateKeys.TotalSupply}", amount);

            SetupMessage(Vault, Miner);

            var proposalId = vault.CreateProposal(amount, Miner, description, (byte)type);

            vault.NextProposalId.Should().Be(proposalId + 1);
            vault.ProposedAmount.Should().Be(amount);

            VerifyLog(new CreateVaultProposalLog
            {
                Id = 1,
                Recipient = Miner,
                Amount = amount,
                Description = description,
                Type = type,
                Status = VaultProposalStatus.Pledge,
                Expiration = block + OneWeek
            }, Times.Once);
        }

        [Fact]
        public void CreateProposal_NewCertificate_Throws_CertificateExists()
        {
            const ulong block = 1000;
            const string description = "create a certificate.";
            UInt256 amount = 100;
            const VaultProposalType type = VaultProposalType.Create;
            var certificate = new VaultCertificate { Amount = amount, VestedBlock = BlocksPerYear + BlocksPerMonth };

            var vault = CreateNewOpdexVault(block);

            State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", certificate);

            vault
                .Invoking(v => v.CreateProposal(amount, Miner, description, (byte)type))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: CERTIFICATE_EXISTS");
        }

        [Fact]
        public void CreateProposal_NewCertificate_Throws_InvalidVaultSupply()
        {
            const ulong block = 1000;
            const string description = "create a certificate.";
            UInt256 vaultSupply = 200;
            UInt256 proposedAmount = 190;
            UInt256 amount = 100;
            const VaultProposalType type = VaultProposalType.Create;

            var vault = CreateNewOpdexVault(block);

            State.SetUInt256($"{VaultStateKeys.TotalSupply}", vaultSupply);
            State.SetUInt256($"{VaultStateKeys.ProposedAmount}", proposedAmount);

            vault
                .Invoking(v => v.CreateProposal(amount, Miner, description, (byte)type))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_VAULT_SUPPLY");
        }

        [Fact]
        public void CreateProposal_RevokeCertificate_Success()
        {
            const string description = "revoke a certificate.";
            UInt256 amount = 100;
            const VaultProposalType type = VaultProposalType.Revoke;

            var certificate = new VaultCertificate { Amount = amount, VestedBlock = BlocksPerYear + BlocksPerMonth };
            var vault = CreateNewOpdexVault(BlocksPerYear);

            State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", certificate);

            SetupMessage(Vault, Miner);

            var proposalId = vault.CreateProposal(amount, Miner, description, (byte)type);

            vault.NextProposalId.Should().Be(proposalId + 1);

            VerifyLog(new CreateVaultProposalLog
            {
                Id = 1,
                Recipient = Miner,
                Amount = amount,
                Description = description,
                Type = type,
                Status = VaultProposalStatus.Pledge,
                Expiration = BlocksPerYear + OneWeek
            }, Times.Once);
        }

        [Theory]
        [InlineData(true, BlocksPerYear, 100)]
        [InlineData(false, 100, 100)]
        [InlineData(false, BlocksPerYear, 99)]
        public void CreateProposal_RevokeCertificate_Throws_InvalidCertificate(bool revoked, ulong vestedBlock, UInt256 amount)
        {
            const ulong block = 1000;
            const string description = "revoke a certificate.";
            const VaultProposalType type = VaultProposalType.Revoke;
            UInt256 revokeAmountSuggested = 100;
            var certificate = new VaultCertificate { Amount = amount, VestedBlock = vestedBlock, Revoked = revoked};

            var vault = CreateNewOpdexVault(block);

            State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", certificate);

            vault
                .Invoking(v => v.CreateProposal(revokeAmountSuggested, Miner, description, (byte)type))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_CERTIFICATE");
        }

        [Fact]
        public void CreateProposal_PledgeMinimum_Success()
        {
            const ulong block = 1000;
            const string description = "change pledge minimum.";
            UInt256 amount = 100;
            const VaultProposalType type = VaultProposalType.PledgeMinimum;

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            var proposalId = vault.CreateProposal(amount, Miner, description, (byte)type);

            vault.NextProposalId.Should().Be(proposalId + 1);

            VerifyLog(new CreateVaultProposalLog
            {
                Id = 1,
                Recipient = Miner,
                Amount = amount,
                Description = description,
                Type = type,
                Status = VaultProposalStatus.Pledge,
                Expiration = block + OneWeek
            }, Times.Once);
        }

        [Fact]
        public void CreateProposal_PledgeMinimum_Throws_InvalidAmount()
        {
            const ulong block = 1000;
            const string description = "change pledge minimum.";
            const VaultProposalType type = VaultProposalType.PledgeMinimum;

            var vault = CreateNewOpdexVault(block);

            vault
                .Invoking(v => v.CreateProposal(10_000_000_000_000_001, Miner, description, (byte)type))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXCESSIVE_AMOUNT");
        }

        [Fact]
        public void CreateProposal_ProposalMinimum_Success()
        {
            const ulong block = 1000;
            const string description = "change proposal minimum.";
            UInt256 amount = 100;
            const VaultProposalType type = VaultProposalType.ProposalMinimum;

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            var proposalId = vault.CreateProposal(amount, Miner, description, (byte)type);

            vault.NextProposalId.Should().Be(proposalId + 1);

            VerifyLog(new CreateVaultProposalLog
            {
                Id = 1,
                Recipient = Miner,
                Amount = amount,
                Description = description,
                Type = type,
                Status = VaultProposalStatus.Pledge,
                Expiration = block + OneWeek
            }, Times.Once);
        }

        [Fact]
        public void CreateProposal_ProposalMinimum_Throws_InvalidAmount()
        {
            const ulong block = 1000;
            const string description = "change proposal minimum.";
            const VaultProposalType type = VaultProposalType.ProposalMinimum;

            var vault = CreateNewOpdexVault(block);

            vault
                .Invoking(v => v.CreateProposal(10_000_000_000_000_001, Miner, description, (byte)type))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXCESSIVE_AMOUNT");
        }

        #endregion

        #region Pledge

        [Theory]
        [InlineData(100, 10, 25, 0)]
        [InlineData(100, 100, 25, 50)]
        [InlineData(100, 10, 99, 75)]
        public void Pledge_Success(ulong minimumProposalPledgeAmount, ulong pledgeAmount, ulong currentProposalPledgeAmount, ulong currentWalletPledgeAmount)
        {
            UInt256 proposalId = 1;
            const ulong block = 1000;
            var expectedWalletPledgeAmount = currentWalletPledgeAmount + pledgeAmount;
            var expectedProposalPledgeAmount = currentProposalPledgeAmount + pledgeAmount;

            var proposal = new VaultProposalDetails
            {
                Amount = pledgeAmount,
                Recipient = Miner,
                Type = VaultProposalType.Create,
                Status = VaultProposalStatus.Pledge,
                Expiration = block + BlocksPerYear,
                Description = "create a certificate.",
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
            vault.GetProposalPledge(proposalId, Miner).Should().Be(expectedWalletPledgeAmount);
            finalProposal.PledgeAmount.Should().Be(expectedProposalPledgeAmount);

            var minimumMet = expectedProposalPledgeAmount > minimumProposalPledgeAmount;
            finalProposal.Status.Should().Be(minimumMet ? VaultProposalStatus.Vote : proposal.Status);
            finalProposal.Expiration.Should().Be(minimumMet ? block + ThreeDays : proposal.Expiration);

            VerifyLog(new VaultProposalPledgeLog
            {
                ProposalId = proposalId,
                Wallet = Miner,
                PledgeAmount = pledgeAmount,
                PledgerAmount = expectedWalletPledgeAmount,
                ProposalPledgeAmount = expectedProposalPledgeAmount
            }, Times.Once);
        }

        [Theory]
        [InlineData(VaultProposalStatus.Vote)]
        [InlineData(VaultProposalStatus.Complete)]
        public void Pledge_Throws_InvalidStatus(VaultProposalStatus status)
        {
            UInt256 proposalId = 1;
            const ulong block = 1000;

            var proposal = new VaultProposalDetails
            {
                Amount = 100,
                Recipient = Miner,
                Type = VaultProposalType.Create,
                Status = status,
                Expiration = block + BlocksPerYear,
                Description = "create a certificate.",
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
            UInt256 proposalId = 1;
            const ulong block = 1000;

            var proposal = new VaultProposalDetails
            {
                Amount = 100,
                Recipient = Miner,
                Type = VaultProposalType.Create,
                Status = VaultProposalStatus.Pledge,
                Expiration = block - 1,
                Description = "create a certificate.",
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

        #endregion

        #region Pledge Withdraw

        [Theory]
        [InlineData(100, 10, 100, 99, VaultProposalStatus.Pledge)]
        [InlineData(100, 100, 100, 100, VaultProposalStatus.Pledge)]
        [InlineData(100, 50, 100, 99, VaultProposalStatus.Vote)]
        [InlineData(100, 100, 100, 100, VaultProposalStatus.Vote)]
        [InlineData(100, 50, 100, 99, VaultProposalStatus.Complete)]
        [InlineData(100, 100, 100, 100, VaultProposalStatus.Complete)]
        public void PledgeWithdraw_Success(ulong currentWalletPledgeAmount, ulong withdrawAmount, ulong currentBlock,
                                           ulong pledgeExpiration, VaultProposalStatus status)
        {
            UInt256 proposalId = 1;
            var expectedWalletPledgeAmount = currentWalletPledgeAmount - withdrawAmount;

            var proposal = new VaultProposalDetails
            {
                Amount = 100,
                Recipient = Miner,
                Type = VaultProposalType.Create,
                Status = status,
                Expiration = pledgeExpiration,
                Description = "create a certificate.",
                YesAmount = 0,
                NoAmount = 0,
                PledgeAmount = currentWalletPledgeAmount + 100
            };

            var vault = CreateNewOpdexVault(currentBlock);

            SetupMessage(Vault, Miner);
            SetupTransfer(Miner, withdrawAmount, TransferResult.Transferred(true));

            State.SetUInt64($"{VaultStateKeys.ProposalPledge}:{proposalId}:{Miner}", currentWalletPledgeAmount);
            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);

            vault.ProposalPledgeWithdraw(proposalId, withdrawAmount);

            var finalProposal = vault.GetProposal(proposalId);
            var finalProposalPledgeAmount = status == VaultProposalStatus.Pledge && pledgeExpiration >= currentBlock
                ? proposal.PledgeAmount - withdrawAmount
                : proposal.PledgeAmount;

            finalProposal.PledgeAmount.Should().Be(finalProposalPledgeAmount);

            vault.GetProposalPledge(proposalId, Miner).Should().Be(expectedWalletPledgeAmount);

            VerifyTransfer(Miner, withdrawAmount, Times.Once);
            VerifyLog(new VaultProposalPledgeWithdrawLog
            {
                ProposalId = proposalId,
                Voter = Miner,
                WithdrawAmount = withdrawAmount,
                PledgerAmount = expectedWalletPledgeAmount,
                ProposalPledgeAmount = finalProposal.PledgeAmount
            }, Times.Once);
        }

        [Fact]
        public void PledgeWithdraw_Throws_InsufficientFunds()
        {
            UInt256 proposalId = 1;
            const ulong block = 1000;
            const ulong currentWalletPledgeAmount = 20;
            const ulong requestedWithdrawAmount = currentWalletPledgeAmount + 1;

            var proposal = new VaultProposalDetails
            {
                Amount = 100,
                Recipient = Miner,
                Type = VaultProposalType.Create,
                Status = VaultProposalStatus.Pledge,
                Expiration = 100,
                Description = "create a certificate.",
                YesAmount = 0,
                NoAmount = 0,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            State.SetUInt64($"{VaultStateKeys.ProposalPledge}:{proposalId}:{Miner}", currentWalletPledgeAmount);
            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);

            vault
                .Invoking(v => v.ProposalPledgeWithdraw(proposalId, requestedWithdrawAmount))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_FUNDS");
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
            UInt256 proposalId = 1;
            const ulong block = 1000;
            const ulong yesAmount = 199;
            const ulong noAmount = 49;
            var totalVoterAmount = existingVoteAmount + voteAmount;

            var proposal = new VaultProposalDetails
            {
                Amount = 25,
                Recipient = Miner,
                Type = VaultProposalType.Create,
                Status = VaultProposalStatus.Vote,
                Expiration = block + ThreeDays,
                Description = "create a certificate.",
                YesAmount = yesAmount,
                NoAmount = noAmount,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            var existingVote = existingVoteAmount > 0
                ? new VaultProposalVote { InFavor = inFavor, Amount = existingVoteAmount }
                : default;

            State.SetStruct($"{VaultStateKeys.ProposalVote}:{proposalId}:{Miner}", existingVote);
            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);

            SetupMessage(Vault, Miner, voteAmount);

            vault.ProposalVote(proposalId, inFavor);

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
        [InlineData(VaultProposalStatus.Pledge)]
        [InlineData(VaultProposalStatus.Complete)]
        [InlineData((VaultProposalStatus)100)]
        public void Vote_Throws_InvalidStatus(VaultProposalStatus status)
        {
            UInt256 proposalId = 1;
            const ulong block = 1000;

            var proposal = new VaultProposalDetails
            {
                Amount = 25,
                Recipient = Miner,
                Type = VaultProposalType.Create,
                Status = status,
                Expiration = block + ThreeDays,
                Description = "create a certificate.",
                YesAmount = 199,
                NoAmount = 49,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);

            SetupMessage(Vault, Miner, 100);

            vault
                .Invoking(v => v.ProposalVote(proposalId, true))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_STATUS");
        }

        [Fact]
        public void Vote_Throws_ProposalExpired()
        {
            UInt256 proposalId = 1;
            const ulong block = 1000;

            var proposal = new VaultProposalDetails
            {
                Amount = 25,
                Recipient = Miner,
                Type = VaultProposalType.Create,
                Status = VaultProposalStatus.Vote,
                Expiration = block - 1,
                Description = "create a certificate.",
                YesAmount = 199,
                NoAmount = 49,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);

            SetupMessage(Vault, Miner, 100);

            vault
                .Invoking(v => v.ProposalVote(proposalId, true))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: PROPOSAL_EXPIRED");
        }

        [Fact]
        public void Vote_Throws_VotedNotInFavor()
        {
            UInt256 proposalId = 1;
            const ulong block = 1000;
            const bool inFavorAttempt = true;
            const bool previousVoteInFavor = false;
            var existingVote = new VaultProposalVote { InFavor = previousVoteInFavor, Amount = 100 };
            var proposal = new VaultProposalDetails
            {
                Amount = 25,
                Recipient = Miner,
                Type = VaultProposalType.Create,
                Status = VaultProposalStatus.Vote,
                Expiration = block,
                Description = "create a certificate.",
                YesAmount = 199,
                NoAmount = 49,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);
            State.SetStruct($"{VaultStateKeys.ProposalVote}:{proposalId}:{Miner}", existingVote);

            SetupMessage(Vault, Miner, 100);

            vault
                .Invoking(v => v.ProposalVote(proposalId, inFavorAttempt))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: ALREADY_VOTED_NOT_IN_FAVOR");
        }

        [Fact]
        public void Vote_Throws_VotedInFavor()
        {
            UInt256 proposalId = 1;
            const ulong block = 1000;
            const bool inFavorAttempt = false;
            const bool previousVoteInFavor = true;
            var existingVote = new VaultProposalVote { InFavor = previousVoteInFavor, Amount = 100 };
            var proposal = new VaultProposalDetails
            {
                Amount = 25,
                Recipient = Miner,
                Type = VaultProposalType.Create,
                Status = VaultProposalStatus.Vote,
                Expiration = block,
                Description = "create a certificate.",
                YesAmount = 199,
                NoAmount = 49,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);
            State.SetStruct($"{VaultStateKeys.ProposalVote}:{proposalId}:{Miner}", existingVote);

            SetupMessage(Vault, Miner, 100);

            vault
                .Invoking(v => v.ProposalVote(proposalId, inFavorAttempt))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: ALREADY_VOTED_IN_FAVOR");
        }

        #endregion

        #region Vote Withdraw

        [Theory]
        [InlineData(true, 100, 10, 100, 99, VaultProposalStatus.Pledge)]
        [InlineData(false, 100, 100, 100, 100, VaultProposalStatus.Pledge)]
        [InlineData(true, 100, 50, 100, 99, VaultProposalStatus.Vote)]
        [InlineData(false, 100, 50, 100, 99, VaultProposalStatus.Vote)]
        [InlineData(true, 100, 100, 100, 100, VaultProposalStatus.Vote)]
        [InlineData(false, 100, 100, 100, 100, VaultProposalStatus.Vote)]
        [InlineData(true, 100, 50, 100, 99, VaultProposalStatus.Complete)]
        [InlineData(false, 100, 100, 100, 100, VaultProposalStatus.Complete)]
        public void VoteWithdraw_Success(bool inFavor, ulong currentWalletVoteAmount, ulong withdrawAmount, ulong currentBlock,
                                         ulong voteExpiration, VaultProposalStatus status)
        {
            UInt256 proposalId = 1;
            var expectedWalletVoteAmount = currentWalletVoteAmount - withdrawAmount;

            var existingVote = new VaultProposalVote { InFavor = inFavor, Amount = currentWalletVoteAmount };
            const ulong currentYesAmount = 500;
            const ulong currentNoAmount = 1000;

            var proposal = new VaultProposalDetails
            {
                Amount = 100,
                Recipient = Miner,
                Type = VaultProposalType.Create,
                Status = status,
                Expiration = voteExpiration,
                Description = "create a certificate.",
                YesAmount = currentYesAmount,
                NoAmount = currentNoAmount,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(currentBlock);

            SetupMessage(Vault, Miner);
            SetupTransfer(Miner, withdrawAmount, TransferResult.Transferred(true));

            State.SetStruct($"{VaultStateKeys.ProposalVote}:{proposalId}:{Miner}", existingVote);
            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);

            vault.ProposalVoteWithdraw(proposalId, withdrawAmount);

            var finalProposal = vault.GetProposal(proposalId);

            ulong finalYesAmount = currentYesAmount;
            ulong finalNoAmount = currentNoAmount;

            if (status == VaultProposalStatus.Vote && voteExpiration >= currentBlock)
            {
                finalYesAmount = inFavor ? currentYesAmount - withdrawAmount : currentYesAmount;
                finalNoAmount = !inFavor ? currentNoAmount - withdrawAmount : currentNoAmount;
            }

            finalProposal.YesAmount.Should().Be(finalYesAmount);
            finalProposal.NoAmount.Should().Be(finalNoAmount);

            vault.GetProposalVote(proposalId, Miner).Amount.Should().Be(expectedWalletVoteAmount);

            VerifyTransfer(Miner, withdrawAmount, Times.Once);
            VerifyLog(new VaultProposalVoteWithdrawLog
            {
                ProposalId = proposalId,
                Voter = Miner,
                WithdrawAmount = withdrawAmount,
                VoterAmount = expectedWalletVoteAmount,
                ProposalYesAmount = finalProposal.YesAmount,
                ProposalNoAmount = finalProposal.NoAmount
            }, Times.Once);
        }

        [Fact]
        public void VoteWithdraw_Throws_InsufficientFunds()
        {
            UInt256 proposalId = 1;
            const ulong block = 1000;
            const ulong currentWalletVoteAmount = 20;
            const ulong requestedWithdrawAmount = currentWalletVoteAmount + 1;

            var existingVote = new VaultProposalVote { InFavor = true, Amount = currentWalletVoteAmount };
            var proposal = new VaultProposalDetails
            {
                Amount = 100,
                Recipient = Miner,
                Type = VaultProposalType.Create,
                Status = VaultProposalStatus.Vote,
                Expiration = 100,
                Description = "create a certificate.",
                YesAmount = 25,
                NoAmount = 70,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            State.SetStruct($"{VaultStateKeys.ProposalVote}:{proposalId}:{Miner}", existingVote);
            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);

            vault
                .Invoking(v => v.ProposalVoteWithdraw(proposalId, requestedWithdrawAmount))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_FUNDS");
        }

        #endregion

        #region Complete Proposal

        [Fact]
        public void CompleteProposal_NewCertificate_Approved_Success()
        {
            UInt256 proposalId = 1;
            const ulong block = 1000;
            const ulong expectedVestedBlock = block + BlocksPerYear;
            UInt256 currentTotalSupply = 100;
            UInt256 expectedTotalSupply = 75;
            UInt256 currentProposedAmount = 75;
            UInt256 expectedProposedAmount = 50;
            UInt256 amount = 25;

            var proposal = new VaultProposalDetails
            {
                Amount = amount,
                Recipient = Miner,
                Type = VaultProposalType.Create,
                Status = VaultProposalStatus.Vote,
                Expiration = block - 1,
                Description = "create a certificate.",
                YesAmount = 199,
                NoAmount = 1,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);
            State.SetUInt256(VaultStateKeys.TotalSupply, currentTotalSupply);
            State.SetUInt256(VaultStateKeys.ProposedAmount, currentProposedAmount);

            vault.CompleteProposal(proposalId);

            var minerCertificate = vault.GetCertificate(Miner);
            minerCertificate.Amount.Should().Be(amount);
            minerCertificate.VestedBlock.Should().Be(expectedVestedBlock);
            minerCertificate.Revoked.Should().BeFalse();

            var proposalResult = vault.GetProposal(proposalId);
            proposalResult.Status.Should().Be(VaultProposalStatus.Complete);

            vault.ProposedAmount.Should().Be(expectedProposedAmount);
            vault.TotalSupply.Should().Be(expectedTotalSupply);

            VerifyLog(new CreateVaultCertificateLog { Owner = Miner, Amount = amount, VestedBlock = expectedVestedBlock }, Times.Once);
            VerifyLog(new CompleteVaultProposalLog { ProposalId = proposalId, Approved = true }, Times.Once);
        }

        [Fact]
        public void CompleteProposal_NewCertificate_NotApproved_Success()
        {
            UInt256 proposalId = 1;
            const ulong block = 1000;
            UInt256 currentTotalSupply = 100;
            UInt256 expectedTotalSupply = 100;
            UInt256 currentProposedAmount = 75;
            UInt256 expectedProposedAmount = 50;
            UInt256 amount = 25;

            var proposal = new VaultProposalDetails
            {
                Amount = amount,
                Recipient = Miner,
                Type = VaultProposalType.Create,
                Status = VaultProposalStatus.Vote,
                Expiration = block - 1,
                Description = "create a certificate.",
                YesAmount = 1,
                NoAmount = 99,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);
            State.SetUInt256(VaultStateKeys.TotalSupply, currentTotalSupply);
            State.SetUInt256(VaultStateKeys.ProposedAmount, currentProposedAmount);

            vault.CompleteProposal(proposalId);

            var minerCertificate = vault.GetCertificate(Miner);
            minerCertificate.Should().BeEquivalentTo(default(VaultCertificate));

            var proposalResult = vault.GetProposal(proposalId);
            proposalResult.Status.Should().Be(VaultProposalStatus.Complete);

            vault.ProposedAmount.Should().Be(expectedProposedAmount);
            vault.TotalSupply.Should().Be(expectedTotalSupply);

            VerifyLog(It.IsAny<CreateVaultCertificateLog>(), Times.Never);
            VerifyLog(new CompleteVaultProposalLog { ProposalId = proposalId, Approved = false }, Times.Once);
        }

        // [Theory]
        // [InlineData(0, 25)]
        // [InlineData(25, 0)]
        // public void CompleteProposal_NewCertificate_Throws_ZeroAmount(UInt256 currentTotalSupply, UInt256 transferAmount)
        // {
        //     const ulong block = 2500;
        //
        //     var vault = CreateNewOpdexVault(block);
        //
        //     State.SetUInt256(VaultStateKeys.TotalSupply, currentTotalSupply);
        //
        //     SetupMessage(Vault, VaultGovernance);
        //
        //     vault
        //         .Invoking(v => v.CreateCertificate(Miner, transferAmount))
        //         .Should().Throw<SmartContractAssertException>()
        //         .WithMessage("OPDEX: INVALID_AMOUNT");
        // }
        //
        // [Fact]
        // public void CompleteProposal_NewCertificate_Throws_CertificateExists()
        // {
        //     const ulong block = 2500;
        //     UInt256 currentTotalSupply = 100;
        //     UInt256 transferAmount = 25;
        //
        //     var existingMinerCertificate = new VaultCertificate { Amount = 10, Revoked = false, VestedBlock = 10000 };
        //
        //     var vault = CreateNewOpdexVault(block);
        //
        //     State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", existingMinerCertificate);
        //     State.SetUInt256(VaultStateKeys.TotalSupply, currentTotalSupply);
        //
        //     SetupMessage(Vault, VaultGovernance);
        //
        //     vault
        //         .Invoking(v => v.CreateCertificate(Miner, transferAmount))
        //         .Should()
        //         .Throw<SmartContractAssertException>()
        //         .WithMessage("OPDEX: CERTIFICATE_EXISTS");
        // }

        [Theory]
        [InlineData((ulong)(BlocksPerYear * .01m), 100, 1)] // vested 1% of the 4 years
        [InlineData((ulong)(BlocksPerYear * .25m), 100, 25)] // vested 25% of the 4 years
        [InlineData((ulong)(BlocksPerYear * .5m), 100, 50)] // vested 50% of the 4 years
        [InlineData((ulong)(BlocksPerYear * .75m), 100, 75)] // vested 75% of the 4 years
        [InlineData((ulong)(BlocksPerYear * .99m), 100, 99)] // vested 99% of the 4 years
        public void CompleteProposal_RevokeCertificate_Approved_Success(ulong block, UInt256 currentAmount, UInt256 expectedAmount)
        {
            UInt256 proposalId = 1;
            UInt256 currentTotalSupply = 75;
            UInt256 expectedTotalSupply = currentTotalSupply + (currentAmount - expectedAmount);
            var existingMinerCertificate = new VaultCertificate {Amount = currentAmount, VestedBlock = BlocksPerYear};

            var proposal = new VaultProposalDetails
            {
                Amount = currentAmount,
                Recipient = Miner,
                Type = VaultProposalType.Revoke,
                Status = VaultProposalStatus.Vote,
                Expiration = block - 1,
                Description = "revoke a certificate.",
                YesAmount = 1320,
                NoAmount = 99,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);
            State.SetUInt256(VaultStateKeys.TotalSupply, currentTotalSupply);
            State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", existingMinerCertificate);

            vault.CompleteProposal(proposalId);

            var minerCertificate = vault.GetCertificate(Miner);
            minerCertificate.Amount.Should().Be(expectedAmount);
            minerCertificate.VestedBlock.Should().Be(BlocksPerYear);
            minerCertificate.Revoked.Should().BeTrue();

            var proposalResult = vault.GetProposal(proposalId);
            proposalResult.Status.Should().Be(VaultProposalStatus.Complete);

            vault.TotalSupply.Should().Be(expectedTotalSupply);

            VerifyLog(new RevokeVaultCertificateLog { Owner = Miner, OldAmount = currentAmount, NewAmount = expectedAmount, VestedBlock = BlocksPerYear }, Times.Once);
            VerifyLog(new CompleteVaultProposalLog { ProposalId = proposalId, Approved = true }, Times.Once);
        }

        [Fact]
        public void CompleteProposal_RevokeCertificate_NotApproved_Success()
        {
            UInt256 proposalId = 1;
            const ulong block = 100;
            UInt256 currentTotalSupply = 75;
            UInt256 expectedTotalSupply = 75;
            UInt256 amount = 100;
            var existingMinerCertificate = new VaultCertificate {Amount = amount, VestedBlock = BlocksPerYear};

            var proposal = new VaultProposalDetails
            {
                Amount = amount,
                Recipient = Miner,
                Type = VaultProposalType.Revoke,
                Status = VaultProposalStatus.Vote,
                Expiration = block - 1,
                Description = "revoke a certificate.",
                YesAmount = 1,
                NoAmount = 99,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);
            State.SetUInt256(VaultStateKeys.TotalSupply, currentTotalSupply);
            State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", existingMinerCertificate);

            vault.CompleteProposal(proposalId);

            var minerCertificate = vault.GetCertificate(Miner);
            minerCertificate.Amount.Should().Be(amount);
            minerCertificate.VestedBlock.Should().Be(BlocksPerYear);
            minerCertificate.Revoked.Should().BeFalse();

            var proposalResult = vault.GetProposal(proposalId);
            proposalResult.Status.Should().Be(VaultProposalStatus.Complete);

            vault.TotalSupply.Should().Be(expectedTotalSupply);

            VerifyLog(It.IsAny<RevokeVaultCertificateLog>(), Times.Never);
            VerifyLog(new CompleteVaultProposalLog { ProposalId = proposalId, Approved = false }, Times.Once);
        }

        [Fact]
        public void CompleteProposal_RevokeCertificate_Throws_CertificateVested()
        {
            UInt256 proposalId = 1;
            const ulong block = 100 + BlocksPerYear;
            UInt256 currentTotalSupply = 75;
            UInt256 amount = 100;
            var existingMinerCertificate = new VaultCertificate {Amount = amount, VestedBlock = BlocksPerYear};

            var proposal = new VaultProposalDetails
            {
                Amount = amount,
                Recipient = Miner,
                Type = VaultProposalType.Revoke,
                Status = VaultProposalStatus.Vote,
                Expiration = 100,
                Description = "revoke a certificate.",
                YesAmount = 101,
                NoAmount = 99,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);
            State.SetUInt256(VaultStateKeys.TotalSupply, currentTotalSupply);
            State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", existingMinerCertificate);

            vault
                .Invoking(v => v.CompleteProposal(proposalId))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: CERTIFICATE_VESTED");
        }

        [Fact]
        public void CompleteProposal_RevokeCertificate_Throws_PreviouslyRevoked()
        {
            UInt256 proposalId = 1;
            const ulong block = 100 + BlocksPerYear;
            UInt256 currentTotalSupply = 75;
            UInt256 amount = 100;
            var existingMinerCertificate = new VaultCertificate {Amount = amount, VestedBlock = BlocksPerYear, Revoked = true};

            var proposal = new VaultProposalDetails
            {
                Amount = amount,
                Recipient = Miner,
                Type = VaultProposalType.Revoke,
                Status = VaultProposalStatus.Vote,
                Expiration = 100,
                Description = "revoke a certificate.",
                YesAmount = 101,
                NoAmount = 99,
                PledgeAmount = 100
            };

            var vault = CreateNewOpdexVault(block);

            SetupMessage(Vault, Miner);

            State.SetStruct($"{VaultStateKeys.Proposal}:{proposalId}", proposal);
            State.SetUInt256(VaultStateKeys.TotalSupply, currentTotalSupply);
            State.SetStruct($"{VaultStateKeys.Certificate}:{Miner}", existingMinerCertificate);

            vault
                .Invoking(v => v.CompleteProposal(proposalId))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: CERTIFICATE_REVOKED");
        }

        [Fact]
        public void CompleteProposal_PledgeMinimum_Approved_Success()
        {
            UInt256 proposalId = 1;
            const ulong block = 100;
            const ulong minimumProposalAmount = 100;
            UInt256 requestedPledgeMinimum = 50;
            const ulong currentPledgeMinimum = 100;

            var proposal = new VaultProposalDetails
            {
                Amount = requestedPledgeMinimum,
                Recipient = Miner,
                Type = VaultProposalType.PledgeMinimum,
                Status = VaultProposalStatus.Vote,
                Expiration = block - 1,
                Description = "revoke a certificate.",
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
            proposalResult.Status.Should().Be(VaultProposalStatus.Complete);

            vault.PledgeMinimum.Should().Be((ulong)requestedPledgeMinimum);

            VerifyLog(new CompleteVaultProposalLog { ProposalId = proposalId, Approved = true }, Times.Once);
        }

        [Fact]
        public void CompleteProposal_PledgeMinimum_NotApproved_Success()
        {
            UInt256 proposalId = 1;
            const ulong block = 100;
            UInt256 requestedPledgeMinimum = 50;
            const ulong pledgeMinimum = 100;

            var proposal = new VaultProposalDetails
            {
                Amount = requestedPledgeMinimum,
                Recipient = Miner,
                Type = VaultProposalType.PledgeMinimum,
                Status = VaultProposalStatus.Vote,
                Expiration = block - 1,
                Description = "revoke a certificate.",
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
            proposalResult.Status.Should().Be(VaultProposalStatus.Complete);

            vault.PledgeMinimum.Should().Be(pledgeMinimum);

            VerifyLog(new CompleteVaultProposalLog { ProposalId = proposalId, Approved = false }, Times.Once);
        }

        [Fact]
        public void CompleteProposal_ProposalMinimum_Approved_Success()
        {
            UInt256 proposalId = 1;
            const ulong block = 100;
            const ulong minimumProposalAmount = 100;
            UInt256 requestedProposalMinimum = 50;

            var proposal = new VaultProposalDetails
            {
                Amount = requestedProposalMinimum,
                Recipient = Miner,
                Type = VaultProposalType.ProposalMinimum,
                Status = VaultProposalStatus.Vote,
                Expiration = block - 1,
                Description = "change proposal minimum.",
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
            proposalResult.Status.Should().Be(VaultProposalStatus.Complete);

            vault.ProposalMinimum.Should().Be((ulong)requestedProposalMinimum);

            VerifyLog(new CompleteVaultProposalLog { ProposalId = proposalId, Approved = true }, Times.Once);
        }

        [Fact]
        public void CompleteProposal_ProposalMinimum_NotApproved_Success()
        {
            UInt256 proposalId = 1;
            const ulong block = 100;
            const ulong minimumProposalAmount = 100;
            UInt256 requestedProposalMinimum = 50;

            var proposal = new VaultProposalDetails
            {
                Amount = requestedProposalMinimum,
                Recipient = Miner,
                Type = VaultProposalType.ProposalMinimum,
                Status = VaultProposalStatus.Vote,
                Expiration = block - 1,
                Description = "change proposal minimum.",
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
            proposalResult.Status.Should().Be(VaultProposalStatus.Complete);

            vault.ProposalMinimum.Should().Be(minimumProposalAmount);

            VerifyLog(new CompleteVaultProposalLog { ProposalId = proposalId, Approved = false }, Times.Once);
        }

        #endregion
    }
}
