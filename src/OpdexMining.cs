using System.Reflection;
using Stratis.SmartContracts;

public class OpdexMining : SmartContract
{
    private static readonly UInt256 BurnAmount = 1000;

    // Todo: Evaluate pros/cons of nobody mining
    // If nothing is done, new coins with no miners will be burnt or for lack of better words, locked here forever
    public OpdexMining(ISmartContractState contractState, Address pair, UInt256 distributionTotal, ulong duration) 
        : base(contractState)
    {
        LastBlockDistributed = Block.Number;
        EndBlock = Block.Number + duration;
        DistributionTotal = distributionTotal - BurnAmount;
        DistributionAmount = distributionTotal % duration + BurnAmount;
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

    public ulong LastBlockDistributed
    {
        get => State.GetUInt64(nameof(LastBlockDistributed));
        private set => State.SetUInt64(nameof(LastBlockDistributed), value);
    }
    
    public UInt256 DistributionTotal
    {
        get => State.GetUInt256(nameof(DistributionTotal));
        private set => State.SetUInt256(nameof(DistributionTotal), value);
    }

    public UInt256 TotalWeight
    {
        get => State.GetUInt256(nameof(TotalWeight));
        private set => State.SetUInt256(nameof(TotalWeight), value);
    }
    
    public UInt256 DistributionAmount
    {
        get => State.GetUInt256(nameof(DistributionAmount));
        private set => State.SetUInt256(nameof(DistributionAmount), value);
    }

    public UInt256 GetWeight(Address owner)
    {
        return State.GetUInt256($"Weight:{owner}");
    }
    
    public void SetWeight(Address owner, UInt256 value)
    {
        State.SetUInt256($"Weight:{owner}", value);
    }
    
    public UInt256 GetWeightK(Address owner)
    {
        return State.GetUInt256($"WeightK:{owner}");
    }
    
    private void SetWeightK(Address owner, UInt256 value)
    {
        State.SetUInt256($"WeightK:{owner}", value);
    }
    
    // Starts mining for a new miner, or if existing miner, withdraws rewards and updates mining weight.
    public void Mine(UInt256 amount)
    {
        var sender = Message.Sender;
        var weight = GetWeight(sender);

        UpdateDistributionAmount();

        var distributionAmount = DistributionAmount;

        if (TotalWeight == 0)
        {
            // Todo: Idk about this...
            TotalWeight = Sqrt(amount * distributionAmount);
        }
        else if (weight > 0)
        {
            WithdrawRewards(); 
        }
        
        SafeTransferFrom(Pair, sender, Address, amount);

        var currentWeight = weight + amount;
        var weightK = CalculateWeightK(currentWeight);
        
        TotalWeight += currentWeight;

        SetWeight(sender, currentWeight);
        SetWeightK(sender, weightK);
        
        Log(new MineEvent
        {
            From = Message.Sender,
            Weight = currentWeight,
            WeightK = weightK
        });
    }
    
    // Stops mining and returns LP tokens and rewards.
    public void Exit()
    {
        WithdrawRewards();

        var sender = Message.Sender;
        var weight = GetWeight(sender);

        SafeTransferTo(Pair, sender, weight);
        SetWeight(sender, 0);
        
        Log(new ExitEvent
        {
            To = sender,
            Weight = weight
        });
    }

    // Keeps mining but withdraws rewards
    public void WithdrawRewards()
    {
        UpdateDistributionAmount();
        
        var sender = Message.Sender;
        var weight = GetWeight(sender);
        var weightK = GetWeightK(sender);
        var currentWeightK = CalculateWeightK(weight);
        var reward = currentWeightK > weightK ? currentWeightK - weightK : 0;

        SafeTransferTo(OPDX, sender, reward);

        DistributionAmount -= reward;
        TotalWeight -= weight;
        
        Log(new WithdrawEvent
        {
            To = sender,
            Reward = reward
        });
    }

    private UInt256 CalculateWeightK(UInt256 liquidity)
    {
        return liquidity * DistributionAmount / TotalWeight;
    }

    private void UpdateDistributionAmount()
    {
        var currentBlock = Block.Number;
        if (currentBlock < EndBlock && currentBlock > LastBlockDistributed)
        {
            var distributionAmount = currentBlock - LastBlockDistributed * BlockReward;
            DistributionAmount += distributionAmount;
            LastBlockDistributed = currentBlock;
        }
    }
    
    private static UInt256 Sqrt(UInt256 value)
    {
        if (value <= 3) return 1;
        
        var result = value;
        var root = value / 2 + 1;
        
        while (root < result) 
        {
            result = root;
            root = (value / root + root) / 2;
        }
        
        return result;
    }
    
    private void SafeTransferTo(Address token, Address to, UInt256 amount)
    {
        if (amount == 0) return;
        
        var result = Call(token, 0, "TransferTo", new object[] {to, amount});
        
        Assert(result.Success && (bool)result.ReturnValue, "OPDEX: INVALID_TRANSFER_TO");
    }
    
    private void SafeTransferFrom(Address token, Address from, Address to, UInt256 amount)
    {
        var result = Call(token, 0, "TransferFrom", new object[] {from, to, amount});
        
        Assert(result.Success && (bool)result.ReturnValue, "OPDEX: INVALID_TRANSFER_FROM");
    }

    public struct MineEvent
    {
        [Index] public Address From;
        public UInt256 Weight;
        public UInt256 WeightK;
    }

    public struct WithdrawEvent
    {
        [Index] public Address To;
        public UInt256 Reward;
    }

    public struct ExitEvent
    {
        [Index] public Address To;
        public UInt256 Weight;
    }
}