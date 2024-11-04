using NUnit.Framework;
using Ryujinx.Audio;
using Ryujinx.Audio.Renderer.Server.MemoryPool;
using System;
using static Ryujinx.Audio.Renderer.Common.BehaviourParameter;
using CpuAddress = System.UInt64;
using DspAddress = System.UInt64;

namespace Ryujinx.Tests.Audio.Renderer.Server
{
    class PoolMapperTests
    {
        private const uint DummyProcessHandle = 0xCAFEBABE;

        [Test]
        public void TestInitializeSystemPool()
        {
            PoolMapper poolMapper = new(DummyProcessHandle, true);
            MemoryPoolState memoryPoolDsp = MemoryPoolState.Create(MemoryPoolState.LocationType.Dsp);
            MemoryPoolState memoryPoolCpu = MemoryPoolState.Create(MemoryPoolState.LocationType.Cpu);

            const CpuAddress CpuAddress = 0x20000;
            const DspAddress DspAddress = CpuAddress; // TODO: DSP LLE
            const ulong CpuSize = 0x1000;

            Assert.That(poolMapper.InitializeSystemPool(ref memoryPoolCpu, CpuAddress, CpuSize), Is.False);
            Assert.That(poolMapper.InitializeSystemPool(ref memoryPoolDsp, CpuAddress, CpuSize), Is.True);

            Assert.That(memoryPoolDsp.CpuAddress, Is.EqualTo(CpuAddress));
            Assert.That(memoryPoolDsp.Size, Is.EqualTo(CpuSize));
            Assert.That(memoryPoolDsp.DspAddress, Is.EqualTo(DspAddress));
        }

        [Test]
        public void TestGetProcessHandle()
        {
            PoolMapper poolMapper = new(DummyProcessHandle, true);
            MemoryPoolState memoryPoolDsp = MemoryPoolState.Create(MemoryPoolState.LocationType.Dsp);
            MemoryPoolState memoryPoolCpu = MemoryPoolState.Create(MemoryPoolState.LocationType.Cpu);

            Assert.That(poolMapper.GetProcessHandle(ref memoryPoolCpu), Is.EqualTo(0xFFFF8001));
            Assert.That(poolMapper.GetProcessHandle(ref memoryPoolDsp), Is.EqualTo(DummyProcessHandle));
        }

        [Test]
        public void TestMappings()
        {
            PoolMapper poolMapper = new(DummyProcessHandle, true);
            MemoryPoolState memoryPoolDsp = MemoryPoolState.Create(MemoryPoolState.LocationType.Dsp);
            MemoryPoolState memoryPoolCpu = MemoryPoolState.Create(MemoryPoolState.LocationType.Cpu);

            const CpuAddress CpuAddress = 0x20000;
            const DspAddress DspAddress = CpuAddress; // TODO: DSP LLE
            const ulong CpuSize = 0x1000;

            memoryPoolDsp.SetCpuAddress(CpuAddress, CpuSize);
            memoryPoolCpu.SetCpuAddress(CpuAddress, CpuSize);

            Assert.That(poolMapper.Map(ref memoryPoolCpu), Is.EqualTo(DspAddress));
            Assert.That(poolMapper.Map(ref memoryPoolDsp), Is.EqualTo(DspAddress));
            Assert.That(memoryPoolDsp.DspAddress, Is.EqualTo(DspAddress));
            Assert.That(poolMapper.Unmap(ref memoryPoolCpu), Is.True);

            memoryPoolDsp.IsUsed = true;
            Assert.That(poolMapper.Unmap(ref memoryPoolDsp), Is.False);
            memoryPoolDsp.IsUsed = false;
            Assert.That(poolMapper.Unmap(ref memoryPoolDsp), Is.True);
        }

        [Test]
        public void TestTryAttachBuffer()
        {
            const CpuAddress CpuAddress = 0x20000;
            const DspAddress DspAddress = CpuAddress; // TODO: DSP LLE
            const ulong CpuSize = 0x1000;

            const int MemoryPoolStateArraySize = 0x10;
            const CpuAddress CpuAddressRegionEnding = CpuAddress * MemoryPoolStateArraySize;

            MemoryPoolState[] memoryPoolStateArray = new MemoryPoolState[MemoryPoolStateArraySize];

            for (int i = 0; i < memoryPoolStateArray.Length; i++)
            {
                memoryPoolStateArray[i] = MemoryPoolState.Create(MemoryPoolState.LocationType.Cpu);
                memoryPoolStateArray[i].SetCpuAddress(CpuAddress + (ulong)i * CpuSize, CpuSize);
            }


            AddressInfo addressInfo = AddressInfo.Create();

            PoolMapper poolMapper = new(DummyProcessHandle, true);

            Assert.That(poolMapper.TryAttachBuffer(out ErrorInfo errorInfo, ref addressInfo, 0, 0), Is.True);

            Assert.That(errorInfo.ErrorCode, Is.EqualTo(ResultCode.InvalidAddressInfo));
            Assert.That(errorInfo.ExtraErrorInfo, Is.EqualTo(0));
            Assert.That(addressInfo.ForceMappedDspAddress, Is.EqualTo(0));

            Assert.That(poolMapper.TryAttachBuffer(out errorInfo, ref addressInfo, CpuAddress, CpuSize), Is.True);

            Assert.That(errorInfo.ErrorCode, Is.EqualTo(ResultCode.InvalidAddressInfo));
            Assert.That(errorInfo.ExtraErrorInfo, Is.EqualTo(CpuAddress));
            Assert.That(addressInfo.ForceMappedDspAddress, Is.EqualTo(DspAddress));

            poolMapper = new PoolMapper(DummyProcessHandle, false);

            Assert.That(poolMapper.TryAttachBuffer(out _, ref addressInfo, 0, 0), Is.False);

            addressInfo.ForceMappedDspAddress = 0;

            Assert.That(poolMapper.TryAttachBuffer(out errorInfo, ref addressInfo, CpuAddress, CpuSize), Is.False);

            Assert.That(errorInfo.ErrorCode, Is.EqualTo(ResultCode.InvalidAddressInfo));
            Assert.That(errorInfo.ExtraErrorInfo, Is.EqualTo(CpuAddress));
            Assert.That(addressInfo.ForceMappedDspAddress, Is.EqualTo(0));

            poolMapper = new PoolMapper(DummyProcessHandle, memoryPoolStateArray.AsMemory(), false);

            Assert.That(poolMapper.TryAttachBuffer(out errorInfo, ref addressInfo, CpuAddressRegionEnding, CpuSize), Is.False);

            Assert.That(errorInfo.ErrorCode, Is.EqualTo(ResultCode.InvalidAddressInfo));
            Assert.That(errorInfo.ExtraErrorInfo, Is.EqualTo(CpuAddressRegionEnding));
            Assert.That(addressInfo.ForceMappedDspAddress, Is.EqualTo(0));
            Assert.That(addressInfo.HasMemoryPoolState, Is.False);

            Assert.That(poolMapper.TryAttachBuffer(out errorInfo, ref addressInfo, CpuAddress, CpuSize), Is.True);

            Assert.That(errorInfo.ErrorCode, Is.EqualTo(ResultCode.Success));
            Assert.That(errorInfo.ExtraErrorInfo, Is.EqualTo(0));
            Assert.That(addressInfo.ForceMappedDspAddress, Is.EqualTo(0));
            Assert.That(addressInfo.HasMemoryPoolState, Is.True);
        }
    }
}
