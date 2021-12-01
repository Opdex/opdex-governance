using Stratis.SmartContracts;

public struct Certificate
{
    public UInt256 Amount;
    public ulong VestedBlock;
    public bool Revoked;
}
