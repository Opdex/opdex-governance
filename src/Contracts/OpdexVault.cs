using System;
using Stratis.SmartContracts;

public class OpdexVault : SmartContract, IOpdexVault
{
    private const ulong OneYear = 60 * 60 * 24 * 365 / 16;
    
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
    public VaultCertificate[] GetCertificates(Address address)
    {
        return State.GetArray<VaultCertificate>($"Certificates:{address}");
    }

    private void SetCertificates(Address address, VaultCertificate[] receipts)
    {
        State.SetArray($"Certificates:{address}", receipts);
    }

    /// <inheritdoc />
    public void NotifyDistribution(UInt256 amount)
    {
        Assert(Message.Sender == Token, "OPDEX: UNAUTHORIZED");
        
        AddCertificate(Owner, amount, Block.Number + OneYear);
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
                lockedCertificates = AddCertificateExecute(lockedCertificates, certificate.Amount, certificate.VestedBlock);
                continue;
            }

            amountToTransfer += certificate.Amount;
            
            Log(new VaultCertificateRedeemedLog {Wallet = Message.Sender, Amount = certificate.Amount});
        }
        
        SetCertificates(Message.Sender, lockedCertificates);
        SafeTransferTo(Token, Message.Sender, amountToTransfer);
    }

    /// <inheritdoc />
    public void RedeemCertificate()
    {
        var certificates = GetCertificates(Message.Sender);

        if (certificates.Length == 0) return;
        
        var updatedCertificates = certificates;

        for (var i = 0; i < certificates.Length; i++)
        {
            if(certificates[i].VestedBlock > Block.Number) continue;
        
            SafeTransferTo(Token, Message.Sender, certificates[i].Amount);

            updatedCertificates = RemoveCertificateAtIndex(certificates, i);
            
            Log(new VaultCertificateRedeemedLog {Wallet = Message.Sender, Amount = certificates[i].Amount});

            break;
        }
        
        SetCertificates(Message.Sender, updatedCertificates);
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
            if (ownerCertificates[i].Amount <= amount || ownerCertificates[i].VestedBlock < Block.Number) continue;
            
            ownerCertificates[i].Amount -= amount;
                
            SetCertificates(owner, ownerCertificates);

            var newVestedBlock = Block.Number + vestingPeriod;
            
            Assert(newVestedBlock >= ownerCertificates[i].VestedBlock, "OPDEX: INSUFFICIENT_VESTING_PERIOD");
            
            AddCertificate(to, amount, newVestedBlock);

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

    private static VaultCertificate[] RemoveCertificateAtIndex(VaultCertificate[] certificates, int index)
    {
        var updatedCertificates = new VaultCertificate[certificates.Length - 1];

        var count = 0;
        
        for (var i = 0; i < certificates.Length; i++)
        {
            if (i == index) continue;
            
            updatedCertificates[count] = certificates[i];
            
            count++;
        }

        return updatedCertificates;
    }

    private void AddCertificate(Address address, UInt256 amount, ulong vestedBlock)
    {
        var certificates = GetCertificates(address);

        certificates = AddCertificateExecute(certificates, amount, vestedBlock);
        
        SetCertificates(address, certificates);
        
        Log(new VaultCertificateCreatedLog{ Wallet = address, Amount = amount, VestedBlock = vestedBlock });
    }

    private static VaultCertificate[] AddCertificateExecute(VaultCertificate[] certificates, UInt256 amount, ulong vestedBlock)
    {
        if (certificates == null) certificates = new VaultCertificate[1];
        else Array.Resize(ref certificates, certificates.Length + 1);

        certificates[certificates.Length - 1] = new VaultCertificate { Amount = amount, VestedBlock = vestedBlock };

        return certificates;
    }
    
    private void SafeTransferTo(Address token, Address to, UInt256 amount)
    {
        if (amount == 0) return;
        
        var result = Call(token, 0, nameof(IOpdexMinedToken.TransferTo), new object[] {to, amount});
        
        Assert(result.Success && (bool)result.ReturnValue, "OPDEX: INVALID_TRANSFER_TO");
    }
}