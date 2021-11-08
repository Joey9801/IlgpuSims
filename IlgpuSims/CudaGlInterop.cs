using System;
using System.Runtime.InteropServices;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace IlgpuSims
{
    public enum CudaGraphicsMapFlags
    {
        None = 0,
        ReadOnly = 1,
        WriteDiscard = 2
    }

    public static class CudaGlInterop
    {
        [DllImport("nvcuda", EntryPoint = "cuGraphicsGLRegisterBuffer")]
        public static extern CudaError RegisterBuffer(
            out IntPtr resource,
            int buffer,
            uint flags);

        [DllImport("nvcuda", EntryPoint = "cuGraphicsMapResources")]
        public static extern CudaError MapResources(
            int count,
            IntPtr resources,
            IntPtr stream);

        [DllImport("nvcuda", EntryPoint = "cuGraphicsUnmapResources")]
        public static extern CudaError UnmapResources(
            int count,
            IntPtr resources,
            IntPtr stream);
        
        [DllImport("nvcuda", EntryPoint = "cuGraphicsResourceGetMappedPointer_v2")]
        public static extern CudaError GetMappedPointer(
            out IntPtr devicePtr,
            out int size,
            IntPtr resource);
    }

    public class CudaGlInteropBuffer : MemoryBuffer
    {
        public static void CudaMemSet<T>(CudaStream stream, byte value, in ArrayView<T> targetView)
            where T : unmanaged
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            if (targetView.GetAcceleratorType() != AcceleratorType.Cuda)
            {
                throw new NotSupportedException();
            }

            var binding = stream.Accelerator.BindScoped();

            CudaException.ThrowIfFailed(
                CudaAPI.CurrentAPI.Memset(
                    targetView.LoadEffectiveAddressAsPtr(),
                    value,
                    new IntPtr(targetView.LengthInBytes),
                    stream));

            binding.Recover();
        }

        public static void CudaCopy<T>(CudaStream stream, in ArrayView<T> sourceView, in ArrayView<T> targetView)
            where T : unmanaged
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            using var binding = stream.Accelerator.BindScoped();

            var sourceType = sourceView.GetAcceleratorType();
            var targetType = targetView.GetAcceleratorType();

            if (sourceType == AcceleratorType.OpenCL ||
                targetType == AcceleratorType.OpenCL)
            {
                throw new NotSupportedException();
            }

            var sourceAddress = sourceView.LoadEffectiveAddressAsPtr();
            var targetAddress = targetView.LoadEffectiveAddressAsPtr();

            var length = new IntPtr(targetView.LengthInBytes);

            // a) Copy from CPU to GPU
            // b) Copy from GPU to CPU
            // c) Copy from GPU to GPU
            CudaException.ThrowIfFailed(
                CudaAPI.CurrentAPI.MemcpyAsync(
                    targetAddress,
                    sourceAddress,
                    length,
                    stream));
        }

        public CudaGlInteropBuffer(Accelerator accelerator, IntPtr ptr, int lengthInBytes)
            : base(accelerator, lengthInBytes, 1)
        {
            NativePtr = ptr;
        }
        
        protected override unsafe void MemSet(
            AcceleratorStream stream,
            byte value,
            in ArrayView<byte> targetView) =>
            CudaMemSet(stream as CudaStream, value, targetView);

        protected override void CopyFrom(
            AcceleratorStream stream,
            in ArrayView<byte> sourceView,
            in ArrayView<byte> targetView) =>
            CudaCopy(stream as CudaStream, sourceView, targetView);

        protected override unsafe void CopyTo(
            AcceleratorStream stream,
            in ArrayView<byte> sourceView,
            in ArrayView<byte> targetView) =>
            CudaCopy(stream as CudaStream, sourceView, targetView);

        protected override void DisposeAcceleratorObject(bool disposing)
        {
            NativePtr = IntPtr.Zero;
        }
    }
}