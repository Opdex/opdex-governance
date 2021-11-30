using Stratis.SmartContracts;

public struct CreateVaultProposalLog
{
    public UInt256 Id;
    public UInt256 Amount;
    public Address Recipient;
    public VaultProposalType Type;
    public VaultProposalStatus Status;
    public ulong Expiration;
    public string Description;
}
