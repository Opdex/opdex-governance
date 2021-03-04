using System.Reflection;
using Stratis.SmartContracts;

public class OpdexMining : SmartContract
{
    public OpdexMining(ISmartContractState contractState, Address pair, UInt256 distributionTotal, ulong duration) 
        : base(contractState)
    {
        EndBlock = Block.Number + duration;
        DistributionTotal = distributionTotal;
        BlockReward = distributionTotal / duration;
        Pair = pair;
        OPDX = Message.Sender;
    }
    
    public Address OPDX
    {
        get => State.GetAddress(nameof(OPDX));
        private set => State.SetAddress(nameof(OPDX), value);
    }

    public Address Pair
    {
        get => State.GetAddress(nameof(Pair));
        private set => State.SetAddress(nameof(Pair), value);
    }

    public UInt256 BlockReward
    {
        get => State.GetUInt256(nameof(BlockReward));
        private set => State.SetUInt256(nameof(BlockReward), value);
    }
    
    public ulong EndBlock
    {
        get => State.GetUInt64(nameof(EndBlock));
        private set => State.SetUInt64(nameof(EndBlock), value);
    }
    
    public UInt256 DistributionTotal
    {
        get => State.GetUInt256(nameof(DistributionTotal));
        private set => State.SetUInt256(nameof(DistributionTotal), value);
    }

    public UInt256 TotalLiquidity
    {
        get => State.GetUInt256(nameof(TotalLiquidity));
        private set => State.SetUInt256(nameof(TotalLiquidity), value);
    }
    
    public UInt256 DistributionAmount
    {
        get => State.GetUInt256(nameof(DistributionAmount));
        private set => State.SetUInt256(nameof(DistributionAmount), value);
    }
    
    public UInt256 GetLiquidity(Address owner)
    {
        return State.GetUInt256($"Liquidity:{owner}");
    }
    
    private void SetLiquidity(Address owner, UInt256 value)
    {
        State.SetUInt256($"Liquidity:{owner}", value);
    }

    // Todo: If already mining and more is added - handle
    // Same thing needs to be solved for staking
    // Maybe withdraw existing rewards, reset and recalc weightK
    public void StartMining(UInt256 amount)
    {
        var transferResponse = Call(Pair, 0, "TransferFrom", new object[] { Message.Sender, Address, amount});
        Assert(transferResponse.Success && (bool)transferResponse.ReturnValue, "OPDEX: UNSUCCESSFUL_TRANSFER_FROM");
        
        // Set senders LP token balance
        SetLiquidity(Message.Sender, amount);

        // Update DistributionAmount to latest
        
        // Set WeightK
        var weightK = amount * DistributionAmount / TotalLiquidity;

        // Update Total Liquidity
        TotalLiquidity += amount;
    }

    public void StopMining()
    {
        
    }
}