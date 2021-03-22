using Stratis.SmartContracts;


/// <summary>
/// Empty base contract for all contracts in this solution to inherit from. Forces the compiler to 
/// respect the contracts equally when looking for the [Deploy] attribute.
/// Without this, the compiler prioritizes direct inheritance of the SmartContract class.
/// </summary>
public class BaseContract : SmartContract
{
    protected BaseContract(ISmartContractState contractState) : base(contractState)
    {
    }
}