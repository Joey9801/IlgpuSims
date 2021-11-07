using System;
using System.Diagnostics;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using OpenTK.Graphics.OpenGL4;

namespace IlgpuSims
{
    public sealed class CudaGlMemoryBuffer : MemoryBuffer
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

        /// <summary>
        /// Constructs a new Cuda buffer.
        /// </summary>
        public CudaGlMemoryBuffer(CudaAccelerator accelerator, Texture glTexture)
            : base(accelerator, glTexture.TotalLength, 1)
        {
            NativePtr = IntPtr.Zero;
            
            if (LengthInBytes == 0)
            {
                return;
            }

            _glResource = IntPtr.Zero;
            CudaException.ThrowIfFailed(CudaGlInterop.RegisterBuffer(
                out _glResource,
                glTexture.PboHandle,
                (uint) CudaGraphicsMapFlags.None));
        }

        private IntPtr _glResource;

        public void Map(CudaStream stream)
        {
            CudaException.ThrowIfFailed(CudaGlInterop.MapResources(
                1, _glResource, stream.StreamPtr));
            CudaException.ThrowIfFailed(CudaGlInterop.GetMappedPointer(
                out var nativePtr, out var size, _glResource));
            Trace.Assert(size == LengthInBytes);

            NativePtr = nativePtr;
        }

        public void Unmap(CudaStream stream)
        {
            CudaException.ThrowIfFailed(CudaGlInterop.UnmapResources(
                1, _glResource, stream.StreamPtr));

            NativePtr = IntPtr.Zero;
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