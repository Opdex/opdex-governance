using Stratis.SmartContracts;

public struct ProposalDetails
{
    public Address Creator;
    public UInt256 Amount;
    public Address Wallet;
    public byte Type;
    public byte Status;
    public ulong Expiration;
    public ulong YesAmount;
    public ulong NoAmount;
    public ulong PledgeAmount;
}
