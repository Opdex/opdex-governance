using Stratis.SmartContracts;

public struct TransferLog
{
    [Index] public Address From;
    [Index] public Address To;
    public UInt256 Amount;
}