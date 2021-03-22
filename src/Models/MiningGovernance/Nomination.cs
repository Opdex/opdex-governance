using Stratis.SmartContracts;

public struct Nomination
{
    public Address StakingToken;
    public UInt256 Weight;
    public bool Funded;
}