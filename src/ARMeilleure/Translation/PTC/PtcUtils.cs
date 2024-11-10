using ARMeilleure.CodeGen;
using ARMeilleure.CodeGen.Linking;
using ARMeilleure.CodeGen.Unwinding;
using ARMeilleure.Common;
using ARMeilleure.Memory;
using Ryujinx.Common;
using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ARMeilleure.Translation.PTC
{

    using Arm64HardwareCapabilities = CodeGen.Arm64.HardwareCapabilities;
    using X86HardwareCapabilities = CodeGen.X86.HardwareCapabilities;

    static class PtcUtils
    {

        public static bool GetEndianness()
        {
            return BitConverter.IsLittleEndian;
        }

        public static PtcFeatureInfo GetFeatureInfo()
        {
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                return new PtcFeatureInfo(
                    (ulong)Arm64HardwareCapabilities.LinuxFeatureInfoHwCap,
                    (ulong)Arm64HardwareCapabilities.LinuxFeatureInfoHwCap2,
                    (ulong)Arm64HardwareCapabilities.MacOsFeatureInfo,
                    0,
                    0);
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                return new PtcFeatureInfo(
                    (ulong)X86HardwareCapabilities.FeatureInfo1Ecx,
                    (ulong)X86HardwareCapabilities.FeatureInfo1Edx,
                    (ulong)X86HardwareCapabilities.FeatureInfo7Ebx,
                    (ulong)X86HardwareCapabilities.FeatureInfo7Ecx,
                    (ulong)X86HardwareCapabilities.Xcr0InfoEax);
            }
            else
            {
                return new PtcFeatureInfo(0, 0, 0, 0, 0);
            }
        }

        public static uint GetOSPlatform()
        {
            uint osPlatform = 0u;

#pragma warning disable IDE0055 // Disable formatting
            osPlatform |= (OperatingSystem.IsFreeBSD() ? 1u : 0u) << 0;
            osPlatform |= (OperatingSystem.IsLinux()   ? 1u : 0u) << 1;
            osPlatform |= (OperatingSystem.IsMacOS()   ? 1u : 0u) << 2;
            osPlatform |= (OperatingSystem.IsWindows() ? 1u : 0u) << 3;
#pragma warning restore IDE0055

            return osPlatform;
        }

        public static Hash128 ComputeHash(IMemoryManager memory, ulong address, ulong guestSize)
        {
            return XXHash128.ComputeHash(memory.GetSpan(address, checked((int)(guestSize))));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<byte> SerializeStructure<T>(T structure) where T : struct
        {
            Span<T> spanT = MemoryMarshal.CreateSpan(ref structure, 1);
            return MemoryMarshal.AsBytes(spanT);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SerializeStructure<T>(Stream stream, T structure) where T : struct
        {
            stream.Write(SerializeStructure(structure));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T DeserializeStructure<T>(Stream stream) where T : struct
        {
            T structure = default;

            Span<T> spanT = MemoryMarshal.CreateSpan(ref structure, 1);

            if ((stream.Length - stream.Position) < spanT.Length)
            {
                throw new EndOfStreamException();
            }

            int bytesCount = stream.Read(MemoryMarshal.AsBytes(spanT));

            if (bytesCount != Unsafe.SizeOf<T>())
            {
                throw new EndOfStreamException();
            }

            return structure;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDeserializeStructure<T>(Stream stream, ref T structure) where T : struct
        {
            Span<T> spanT = MemoryMarshal.CreateSpan(ref structure, 1);

            if ((stream.Length - stream.Position) < spanT.Length)
            {
                return false;
            }

            int bytesCount = stream.Read(MemoryMarshal.AsBytes(spanT));

            if (bytesCount != Unsafe.SizeOf<T>())
            {
                return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SerializeHashedStructure<T>(Stream stream, T structure) where T : struct
        {
            Hash128 hash = XXHash128.ComputeHash(SerializeStructure(structure));

            SerializeStructure(stream, structure);
            SerializeStructure(stream, hash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDeserializeHashedStructure<T>(Stream stream, out T structure) where T : struct
        {
            structure = default;

            if (!TryDeserializeStructure(stream, ref structure))
            {
                return false;
            }

            Hash128 hash = default;

            if (!TryDeserializeStructure(stream, ref hash))
            {
                return false;
            }

            Hash128 actualHash = XXHash128.ComputeHash(SerializeStructure(structure));

            return hash == actualHash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDeserializeHashedStructureByRef<T>(Stream stream, ref T structure) where T : struct
        {
            if (!TryDeserializeStructure(stream, ref structure))
            {
                return false;
            }

            Hash128 hash = default;

            if (!TryDeserializeStructure(stream, ref hash))
            {
                return false;
            }

            Hash128 actualHash = XXHash128.ComputeHash(SerializeStructure(structure));

            return hash == actualHash;
        }

        public static void PatchCode(Translator translator, Span<byte> code, RelocEntry[] relocEntries, out Counter<uint> callCounter)
        {
            callCounter = null;

            foreach (RelocEntry relocEntry in relocEntries)
            {
                nint? imm = null;
                Symbol symbol = relocEntry.Symbol;

                if (symbol.Type == SymbolType.FunctionTable)
                {
                    ulong guestAddress = symbol.Value;

                    if (translator.FunctionTable.IsValid(guestAddress))
                    {
                        unsafe
                        {
                            imm = (nint)Unsafe.AsPointer(ref translator.FunctionTable.GetValue(guestAddress));
                        }
                    }
                }
                else if (symbol.Type == SymbolType.DelegateTable)
                {
                    int index = (int)symbol.Value;

                    if (Delegates.TryGetDelegateFuncPtrByIndex(index, out nint funcPtr))
                    {
                        imm = funcPtr;
                    }
                }
                else if (symbol == Translator.PageTableSymbol)
                {
                    imm = translator.Memory.PageTablePointer;
                }
                else if (symbol == Translator.CountTableSymbol)
                {
                    callCounter ??= new Counter<uint>(translator.CountTable);

                    unsafe
                    {
                        imm = (nint)Unsafe.AsPointer(ref callCounter.Value);
                    }
                }
                else if (symbol == Translator.DispatchStubSymbol)
                {
                    imm = translator.Stubs.DispatchStub;
                }
                else if (symbol == Translator.FunctionTableSymbol)
                {
                    imm = translator.FunctionTable.Base;
                }

                if (imm == null)
                {
                    throw new Exception($"Unexpected reloc entry {relocEntry}.");
                }

                BinaryPrimitives.WriteUInt64LittleEndian(code.Slice(relocEntry.Position, 8), (ulong)imm.Value);
            }
        }

        public static TranslatedFunction FastTranslate(
            byte[] code,
            Counter<uint> callCounter,
            ulong guestSize,
            UnwindInfo unwindInfo,
            bool highCq)
        {
            var cFunc = new CompiledFunction(code, unwindInfo, RelocInfo.Empty);
            var gFunc = cFunc.MapWithPointer<GuestFunction>(out IntPtr gFuncPointer);

            return new TranslatedFunction(gFunc, gFuncPointer, callCounter, guestSize, highCq);
        }

        public static byte[] Compress(byte[] data)
        {
            using MemoryStream outputStream = new MemoryStream();

            using (DeflateStream compressionStream = new DeflateStream(outputStream, CompressionLevel.Fastest))
            {
                compressionStream.Write(data, 0, data.Length);
            }

            return outputStream.ToArray();
        }

        public static byte[] Decompress(byte[] data)
        {
            using MemoryStream inputStream = new MemoryStream(data);
            using MemoryStream outputStream = new MemoryStream();

            using (DeflateStream compressionStream = new DeflateStream(inputStream, CompressionMode.Decompress))
            {
                compressionStream.CopyTo(outputStream);
                compressionStream.Flush();
            }

            return outputStream.ToArray();
        }

    }

}
