using System;
using System.Linq;
using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Networks;

namespace OpdexTokenTests.Base
{
    public class TestBase
    {
        private readonly Mock<ISmartContractState> _mockContractState;
        private readonly Mock<IContractLogger> _mockContractLogger;
        private readonly Mock<IInternalTransactionExecutor> _mockInternalExecutor;
        protected readonly ISerializer Serializer;
        protected readonly Address MiningContract;
        protected readonly Address Pair;
        protected readonly Address OPDX;
        protected readonly Address Miner;
        protected readonly InMemoryState PersistentState;

        protected TestBase()
        {
            PersistentState = new InMemoryState();
            _mockContractLogger = new Mock<IContractLogger>();
            _mockContractState = new Mock<ISmartContractState>();
            _mockInternalExecutor = new Mock<IInternalTransactionExecutor>();
            Serializer = new Serializer(new ContractPrimitiveSerializer(new SmartContractsPoARegTest()));
            _mockContractState.Setup(x => x.PersistentState).Returns(PersistentState);
            _mockContractState.Setup(x => x.ContractLogger).Returns(_mockContractLogger.Object);
            _mockContractState.Setup(x => x.InternalTransactionExecutor).Returns(_mockInternalExecutor.Object);
            _mockContractState.Setup(x => x.Serializer).Returns(Serializer);
            MiningContract = "0x0000000000000000000000000000000000000001".HexToAddress();
            Pair = "0x0000000000000000000000000000000000000002".HexToAddress();
            OPDX = "0x0000000000000000000000000000000000000003".HexToAddress();
            Miner = "0x0000000000000000000000000000000000000004".HexToAddress();
        }

        // protected OpdexMining CreateNewOpdexMiner(UInt256 amountToDistribute, ulong duration = 100)
        // {
        //     if (amountToDistribute == 0)
        //     {
        //         amountToDistribute = 20_000_000_000_000_000; // 200 million
        //     }
        //     
        //     _mockContractState.Setup(x => x.Message).Returns(new Message(MiningContract, OPDX, 0));
        //     _mockContractState.Setup(x => x.Block.Number).Returns(() => 10);
        //     
        //     SetupBalance(0);
        //     
        //     return new OpdexMining(_mockContractState.Object, Pair, amountToDistribute, duration);
        // }
        
        // protected OpdexPair CreateNewOpdexPair(ulong balance = 0)
        // {
        //     _mockContractState.Setup(x => x.Message).Returns(new Message(Pair, Controller, 0));
        //     PersistentState.SetContract(StakeToken, true);
        //     SetupBalance(balance);
        //     return new OpdexPair(_mockContractState.Object, Token, StakeToken);
        // }

        protected void SetupMessage(Address contractAddress, Address sender, ulong value = 0)
        {
            _mockContractState.Setup(x => x.Message).Returns(new Message(contractAddress, sender, value));
            var balance = _mockContractState.Object.GetBalance();
            SetupBalance(balance + value);
        }

        protected void SetupBalance(ulong balance)
        {
            _mockContractState.Setup(x => x.GetBalance).Returns(() => balance);
        }

        protected void SetupCall(Address to, ulong amountToTransfer, string methodName, object[] parameters, TransferResult result, Action callback = null)
        {
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, to, amountToTransfer, methodName, It.Is<object[]>(p => ValidateParameters(parameters, p)), It.IsAny<ulong>()))
                .Returns(result)
                .Callback(() =>
                {
                    // Adjusts for CRS sent out with a Call
                    var balance = _mockContractState.Object.GetBalance();
                    _mockContractState.Setup(x => x.GetBalance).Returns(() => checked(balance - amountToTransfer));

                    // Optional callback for scenarios where CRS or SRC funds are transferred back within the call being setup ^
                    callback?.Invoke();
                });
        }

        protected void SetupTransfer(Address to, ulong value, TransferResult result)
        {
            _mockInternalExecutor
                .Setup(x => x.Transfer(_mockContractState.Object, to, value))
                .Returns(result)
                .Callback(() =>
                {
                    var balance = _mockContractState.Object.GetBalance();
                    _mockContractState.Setup(x => x.GetBalance).Returns(() => checked(balance - value));
                });
        }

        protected void SetupCreate<T>(CreateResult result, ulong amount = 0, object[] parameters = null)
        {
            _mockInternalExecutor
                .Setup(x => x.Create<T>(_mockContractState.Object, amount, parameters, It.IsAny<ulong>()))
                .Returns(result);
        }

        protected void VerifyCall(Address addressTo, ulong amountToTransfer, string methodName, object[] parameters, Func<Times> times)
        {
            _mockInternalExecutor.Verify(x => x.Call(_mockContractState.Object, addressTo, amountToTransfer, methodName, It.Is<object[]>(p => ValidateParameters(parameters, p)), 0ul), times);
        }

        protected void VerifyTransfer(Address to, ulong value, Func<Times> times)
        {
            _mockInternalExecutor.Verify(x => x.Transfer(_mockContractState.Object, to, value), times);
        }

        protected void VerifyLog<T>(T expectedLog, Func<Times> times)
            where T : struct
        {
            _mockContractLogger.Verify(x => x.Log(_mockContractState.Object, expectedLog), times);
        }

        private static bool ValidateParameters(object[] expected, object[] actual)
        {
            if (expected == null && actual == null)
            {
                return true;
            }

            if (actual == null ^ expected == null)
            {
                return false;
            }

            for (var i = 0; i < expected.Length; i++)
            {
                var expectedParam = expected[i];
                var actualParam = actual[i];
                    
                if (expected.GetType().IsArray)
                {
                    var expectedArray = expectedParam as byte[] ?? new byte[0];
                    var actualArray = actualParam as byte[] ?? new byte[0];
                        
                    if (expectedArray.Where((t, b) => !t.Equals(actualArray[b])).Any())
                    {
                        return false;
                    }
                }
                else
                {
                    if (!expectedParam.Equals(actualParam))
                    {
                        return false;
                    }
                }
            }
                
            return true;
        }
    }
}