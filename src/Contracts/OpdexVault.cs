using System;
using Stratis.SmartContracts;

/// <summary>
/// A smart contract that locks tokens for a specified vesting period. 
/// </summary>
public class OpdexVault : SmartContract, IOpdexVault
{
    private const uint MaximumCertificates = 10;
    
    /// <summary>
    /// Constructor initializing an empty vault for locking tokens to be vested.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="token">The locked SRC token.</param>
    /// <param name="owner">The vault owner.</param>
    /// <param name="distributionPeriod">The length in blocks of the vesting period.</param>
    public OpdexVault(ISmartContractState state, Address token, Address owner, ulong distributionPeriod) : base(state)
    {
        Token = token;
        Owner = owner;
        VestingDuration = distributionPeriod * 4;
    }

    /// <inheritdoc />
    public ulong Genesis
    {
        get => State.GetUInt64(nameof(Genesis));
        private set => State.SetUInt64(nameof(Genesis), value);
    }
    
    /// <inheritdoc />
    public ulong VestingDuration
    {
        get => State.GetUInt64(nameof(VestingDuration));
        private set => State.SetUInt64(nameof(VestingDuration), value);
    }

    /// <inheritdoc />
    public Address Token
    {
        get => State.GetAddress(nameof(Token));
        private set => State.SetAddress(nameof(Token), value);
    }
    
    /// <inheritdoc />
    public Address Owner
    {
        get => State.GetAddress(nameof(Owner));
        private set => State.SetAddress(nameof(Owner), value);
    }
    
    /// <inheritdoc />
    public UInt256 TotalSupply
    {
        get => State.GetUInt256(nameof(TotalSupply));
        private set => State.SetUInt256(nameof(TotalSupply), value);
    }
    
    /// <inheritdoc />
    public VaultCertificate[] GetCertificates(Address wallet)
    {
        return State.GetArray<VaultCertificate>($"Certificates:{wallet}") ?? new VaultCertificate[0];
    }

    private void SetCertificates(Address wallet, VaultCertificate[] certificates)
    {
        State.SetArray($"Certificates:{wallet}", certificates);
    }

    /// <inheritdoc />
    public void NotifyDistribution(UInt256 amount)
    {
        Assert(Message.Sender == Token, "OPDEX: UNAUTHORIZED");

        TotalSupply += amount;

        if (Genesis == 0) Genesis = Block.Number;
    }

    /// <inheritdoc />
    public void CreateCertificate(Address to, UInt256 amount)
    {
        var owner = Owner;
        var vestingDuration = VestingDuration;
        
        Assert(Message.Sender == owner, "OPDEX: UNAUTHORIZED");
        Assert(to != owner, "OPDEX: INVALID_CERTIFICATE_HOLDER");
        Assert(amount > 0 && amount <= TotalSupply, "OPDEX: INVALID_AMOUNT");
        Assert(Block.Number < Genesis + vestingDuration, "OPDEX: TOKENS_BURNED");
        
        var certificates = GetCertificates(to);

        Assert(certificates.Length < MaximumCertificates, "OPDEX: CERTIFICATE_LIMIT_REACHED");
        
        var vestedBlock = Block.Number + vestingDuration;

        certificates = InsertCertificate(certificates, amount, vestedBlock, false);
        
        SetCertificates(to, certificates);

        TotalSupply -= amount;
        
        Log(new CreateVaultCertificateLog{ Owner = to, Amount = amount, VestedBlock = vestedBlock });
    }

    /// <inheritdoc />
    public void RedeemCertificates()
    {
        var certificates = GetCertificates(Message.Sender);
        var lockedCertificates = new VaultCertificate[0];
        var amountToTransfer = UInt256.Zero;

        foreach (var certificate in certificates)
        {
            if (certificate.VestedBlock > Block.Number)
            {
                lockedCertificates = InsertCertificate(lockedCertificates, certificate.Amount, certificate.VestedBlock, certificate.Revoked);
                continue;
            }

            amountToTransfer += certificate.Amount;
            
            Log(new RedeemVaultCertificateLog {Owner = Message.Sender, Amount = certificate.Amount, VestedBlock = certificate.VestedBlock});
        }
        
        SetCertificates(Message.Sender, lockedCertificates);
        SafeTransferTo(Token, Message.Sender, amountToTransfer);
    }

    /// <inheritdoc />
    public void RevokeCertificates(Address wallet)
    {
        Assert(Message.Sender == Owner, "OPDEX: UNAUTHORIZED");
        
        var certificates = GetCertificates(wallet);
        var vestingDuration = VestingDuration;

        for (var i = 0; i < certificates.Length; i++)
        {
            var vestingAmount = certificates[i].Amount;
            var vestedBlock = certificates[i].VestedBlock;
            var revoked = certificates[i].Revoked;
            
            if (revoked || vestedBlock <= Block.Number) continue;

            var vestingBlock = vestedBlock - vestingDuration;
            var vestedBlocks = Block.Number - vestingBlock;
            var percentageOffset = (UInt256)100;
            var divisor = vestingDuration * percentageOffset / vestedBlocks;
            var newAmount = vestingAmount * percentageOffset / divisor;
            
            certificates[i].Amount = newAmount;
            certificates[i].Revoked = true;

            TotalSupply += (vestingAmount - newAmount);

            Log(new RevokeVaultCertificateLog {Owner = wallet, OldAmount = vestingAmount, NewAmount = newAmount, VestedBlock = certificates[i].VestedBlock});
        }
        
        SetCertificates(wallet, certificates);
    }
    
    /// <inheritdoc />
    public void SetOwner(Address owner)
    {
        Assert(Message.Sender == Owner, "OPDEX: UNAUTHORIZED");
        
        Owner = owner;
        
        Log(new ChangeVaultOwnerLog { From = Message.Sender, To = owner });
    }

    private static VaultCertificate[] InsertCertificate(VaultCertificate[] certificates, UInt256 amount, ulong vestedBlock, bool revoked)
    {
        var originalLength = certificates.Length;
        
        Array.Resize(ref certificates, originalLength + 1);

        certificates[originalLength] = new VaultCertificate { Amount = amount, VestedBlock = vestedBlock, Revoked = revoked};

        return certificates;
    }
    
    private void SafeTransferTo(Address token, Address to, UInt256 amount)
    {
        if (amount == 0) return;
        
        var result = Call(token, 0, nameof(IOpdexMinedToken.TransferTo), new object[] {to, amount});
        
        Assert(result.Success && (bool)result.ReturnValue, "OPDEX: INVALID_TRANSFER_TO");
    }
}