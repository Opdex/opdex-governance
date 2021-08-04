using System;
using Moq;
using Stratis.Bitcoin.Features.PoA;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;

namespace OpdexGovernanceTests.Base
{
    public class TestBase
    {
        private readonly Mock<ISmartContractState> _mockContractState;
        private readonly Mock<IContractLogger> _mockContractLogger;
        private readonly Mock<IInternalTransactionExecutor> _mockInternalExecutor;
        protected readonly ISerializer Serializer;
        protected readonly Address ODX;
        protected readonly Address MiningGovernance;
        protected readonly Address MiningPool1;
        protected readonly Address MiningPool2;
        protected readonly Address MiningPool3;
        protected readonly Address MiningPool4;
        protected readonly Address MiningPool5;
        protected readonly Address Owner;
        protected readonly Address Vault;
        protected readonly Address Miner;
        protected readonly Address Pool1;
        protected readonly Address Pool2;
        protected readonly Address Pool3;
        protected readonly Address Pool4;
        protected readonly Address Pool5;
        protected readonly InMemoryState State;
        protected const ulong BlocksPerYear = 60 * 60 * 24 * 365 / 16;
        protected const ulong BlocksPerMonth = BlocksPerYear / 12;

        protected TestBase()
        {
            State = new InMemoryState();
            Serializer = new Serializer(new ContractPrimitiveSerializer(new PoANetwork()));
            _mockContractLogger = new Mock<IContractLogger>();
            _mockContractState = new Mock<ISmartContractState>();
            _mockInternalExecutor = new Mock<IInternalTransactionExecutor>();
            _mockContractState.Setup(x => x.PersistentState).Returns(State);
            _mockContractState.Setup(x => x.ContractLogger).Returns(_mockContractLogger.Object);
            _mockContractState.Setup(x => x.InternalTransactionExecutor).Returns(_mockInternalExecutor.Object);
            _mockContractState.Setup(x => x.Serializer).Returns(Serializer);
            ODX = "0x0000000000000000000000000000000000000001".HexToAddress();
            MiningGovernance = "0x0000000000000000000000000000000000000002".HexToAddress();
            MiningPool1 = "0x0000000000000000000000000000000000000003".HexToAddress();
            MiningPool2 = "0x0000000000000000000000000000000000000004".HexToAddress();
            MiningPool3 = "0x0000000000000000000000000000000000000005".HexToAddress();
            MiningPool4 = "0x0000000000000000000000000000000000000006".HexToAddress();
            MiningPool5 = "0x0000000000000000000000000000000000000007".HexToAddress();
            Owner = "0x0000000000000000000000000000000000000008".HexToAddress();
            Miner = "0x0000000000000000000000000000000000000009".HexToAddress();
            Pool1 = "0x0000000000000000000000000000000000000010".HexToAddress();
            Pool2 = "0x0000000000000000000000000000000000000011".HexToAddress();
            Pool3 = "0x0000000000000000000000000000000000000012".HexToAddress();
            Pool4 = "0x0000000000000000000000000000000000000013".HexToAddress();
            Pool5 = "0x0000000000000000000000000000000000000014".HexToAddress();
            Vault = "0x0000000000000000000000000000000000000015".HexToAddress();
        }

        protected IOpdexMiningGovernance CreateNewOpdexMiningGovernance(ulong block = 10)
        {
            _mockContractState.Setup(x => x.Message).Returns(new Message(MiningGovernance, ODX, 0));
            _mockContractState.Setup(x => x.Block.Number).Returns(() => block);

            SetupBalance(0);

            return new OpdexMiningGovernance(_mockContractState.Object, ODX, BlocksPerMonth);
        }

        protected IOpdexMinedToken CreateNewOpdexToken(byte[] ownerSchedule, byte[] miningSchedule, ulong block = 10)
        {
            _mockContractState.Setup(x => x.Message).Returns(new Message(ODX, Owner, 0));

            SetupBalance(0);
            SetupBlock(block);
            SetupCreate<OpdexMiningGovernance>(CreateResult.Succeeded(MiningGovernance), 0ul, new object[] {ODX, BlocksPerMonth});
            SetupCreate<OpdexVault>(CreateResult.Succeeded(Vault), 0ul, new object[] {ODX, Owner, BlocksPerYear * 4});

            return new OpdexMinedToken(_mockContractState.Object, "Opdex", "ODX", ownerSchedule, miningSchedule, BlocksPerYear);
        }

        protected IOpdexVault CreateNewOpdexVault(ulong block = 10)
        {
            _mockContractState.Setup(x => x.Message).Returns(new Message(MiningPool1, ODX, 0));

            SetupBalance(0);
            SetupBlock(block);

            return new OpdexVault(_mockContractState.Object, ODX, Owner, BlocksPerYear * 4);
        }

        protected void SetupMessage(Address contractAddress, Address sender, ulong value = 0)
        {
            _mockContractState.Setup(x => x.Message).Returns(new Message(contractAddress, sender, value));
            var balance = value > 0 ? _mockContractState.Object.GetBalance() : 0;
            SetupBalance(balance + value);
        }

        protected void SetupBalance(ulong balance)
        {
            _mockContractState.Setup(x => x.GetBalance).Returns(() => balance);
        }

        protected void SetupBlock(ulong block)
        {
            _mockContractState.Setup(x => x.Block.Number).Returns(() => block);
        }

        protected void SetupCall(Address to, ulong amountToTransfer, string methodName, object[] parameters, TransferResult result, Action callback = null)
        {
            _mockInternalExecutor
                .Setup(x =>
                    x.Call(_mockContractState.Object, to, amountToTransfer, methodName,
                        It.Is<object[]>(p => ValidateParameters(parameters, p)), It.IsAny<ulong>()))
                .Returns(result)
                .Callback(() => SetupContractCallMockCallback(amountToTransfer, callback));
        }

        private void SetupContractCallMockCallback(ulong amountToTransfer, Action callback = null)
        {
            // Adjusts for CRS sent out with a Call
            var balance = _mockContractState.Object.GetBalance();
            _mockContractState.Setup(x => x.GetBalance).Returns(() => checked(balance - amountToTransfer));

            // Optional callback for scenarios where CRS or SRC funds are transferred back within the call being setup ^
            callback?.Invoke();
        }

        protected void SetupCreate<T>(CreateResult result, ulong amount = 0, object[] parameters = null)
        {
            _mockInternalExecutor
                .Setup(x => x.Create<T>(_mockContractState.Object, amount, parameters, It.IsAny<ulong>()))
                .Returns(result);
        }

        protected void VerifyCall(Address addressTo, ulong amountToTransfer, string methodName, object[] parameters, Func<Times> times)
        {
            _mockInternalExecutor.Verify(x =>
                x.Call(_mockContractState.Object, addressTo, amountToTransfer, methodName,
                    It.Is<object[]>(p => ValidateParameters(parameters, p)), 0ul), times);
        }

        protected void VerifyTransfer(Address to, ulong value, Func<Times> times)
        {
            _mockInternalExecutor.Verify(x => x.Transfer(_mockContractState.Object, to, value), times);
        }

        protected void VerifyCreate<T>(ulong amountToTransfer, object[] createParams, Func<Times> times)
        {
            _mockInternalExecutor.Verify(x => x.Create<T>(_mockContractState.Object, amountToTransfer, createParams, 0ul), times);
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

            if (expected == null ^ actual == null)
            {
                return false;
            }

            for (var i = 0; i < expected.Length; i++)
            {
                if (expected[i].ToString() != actual[i].ToString())
                {
                    return false;
                }
            }

            return true;
        }
    }
}