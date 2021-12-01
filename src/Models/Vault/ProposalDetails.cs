using Stratis.SmartContracts;

public struct ProposalDetails
{
    public UInt256 Amount;
    public Address Wallet;
    public byte Type;
    public byte Status;
    public ulong Expiration;
    public ulong YesAmount;
    public ulong NoAmount;
    public ulong PledgeAmount;
}
