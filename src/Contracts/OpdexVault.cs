using System;
using Stratis.SmartContracts;

public class OpdexVault : SmartContract, IOpdexVault
{
    private const ulong OneYear = 60 * 60 * 24 * 365 / 16;
    private const uint MaximumCertificates = 10;
    
    /// <summary>
    /// Constructor initializing an empty vault for an SRC token assigned to an owner.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="token">The locked SRC token.</param>
    /// <param name="owner">The vault owner.</param>
    public OpdexVault(ISmartContractState state, Address token, Address owner) : base(state)
    {
        Token = token;
        Owner = owner;
        SetCertificates(owner, new VaultCertificate[0]);
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
    public VaultCertificate[] GetCertificates(Address wallet)
    {
        return State.GetArray<VaultCertificate>($"Certificates:{wallet}") ?? new VaultCertificate[0];
    }

    private void SetCertificates(Address wallet, VaultCertificate[] receipts)
    {
        State.SetArray($"Certificates:{wallet}", receipts);
    }

    /// <inheritdoc />
    public void NotifyDistribution(UInt256 amount)
    {
        Assert(Message.Sender == Token, "OPDEX: UNAUTHORIZED");

        var vestedBlock = Block.Number + OneYear;
        
        // Intentional burn on failure
        AddCertificate(Owner, amount, vestedBlock);
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
                lockedCertificates = InsertCertificate(lockedCertificates, certificate.Amount, certificate.VestedBlock);
                continue;
            }

            amountToTransfer += certificate.Amount;
            
            Log(new VaultCertificateRedeemedLog {Owner = Message.Sender, Amount = certificate.Amount});
        }
        
        SetCertificates(Message.Sender, lockedCertificates);
        SafeTransferTo(Token, Message.Sender, amountToTransfer);
    }
    
    /// <inheritdoc />
    public bool CreateCertificate(Address to, UInt256 amount, ulong vestingPeriod)
    {
        var owner = Owner;
        
        Assert(Message.Sender == owner, "OPDEX: UNAUTHORIZED");
        Assert(amount > 0, "OPDEX: ZERO_AMOUNT");

        var ownerCertificates = GetCertificates(owner);
        
        for (var i = 0; i < ownerCertificates.Length; i++)
        {
            if (ownerCertificates[i].Amount < amount) continue;
            
            ownerCertificates[i].Amount -= amount;
            
            var newVestedBlock = Block.Number + vestingPeriod;
            
            Assert(newVestedBlock >= ownerCertificates[i].VestedBlock, "OPDEX: INSUFFICIENT_VESTING_PERIOD");
            
            var created = AddCertificate(to, amount, newVestedBlock);

            if (!created) return false;
            
            SetCertificates(owner, ownerCertificates);
            
            Log(new VaultCertificateUpdatedLog {Owner = owner, Amount = ownerCertificates[i].Amount, VestedBlock = ownerCertificates[i].VestedBlock});

            return true;
        }

        return false;
    }
    
    /// <inheritdoc />
    public void SetOwner(Address owner)
    {
        Assert(Message.Sender == Owner, "OPDEX: UNAUTHORIZED");
        
        Owner = owner;
        
        Log(new VaultOwnerChangeLog { From = Message.Sender, To = owner });
    }

    private bool AddCertificate(Address address, UInt256 amount, ulong vestedBlock)
    {
        var certificates = GetCertificates(address);

        if (certificates.Length >= MaximumCertificates) return false;

        certificates = InsertCertificate(certificates, amount, vestedBlock);
        
        SetCertificates(address, certificates);
        
        Log(new VaultCertificateCreatedLog{ Owner = address, Amount = amount, VestedBlock = vestedBlock });

        return true;
    }

    private static VaultCertificate[] InsertCertificate(VaultCertificate[] certificates, UInt256 amount, ulong vestedBlock)
    {
        var originalLength = certificates.Length;
        
        Array.Resize(ref certificates, originalLength + 1);

        certificates[originalLength] = new VaultCertificate { Amount = amount, VestedBlock = vestedBlock };

        return certificates;
    }
    
    private void SafeTransferTo(Address token, Address to, UInt256 amount)
    {
        if (amount == 0) return;
        
        var result = Call(token, 0, nameof(IOpdexMinedToken.TransferTo), new object[] {to, amount});
        
        Assert(result.Success && (bool)result.ReturnValue, "OPDEX: INVALID_TRANSFER_TO");
    }
}