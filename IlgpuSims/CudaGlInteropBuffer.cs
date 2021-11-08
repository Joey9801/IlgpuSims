using System;
using System.Diagnostics;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace IlgpuSims
{
    /// <summary>
    ///     A 2D floating point buffer that can be read from + written to by both OpenGL and CUDA
    /// </summary>
    public sealed class CudaGlInteropBuffer : MemoryBuffer
    {
        public readonly Vector2i Size;
        private IntPtr _cudaResource;
        private readonly int _glPboHandle;
        private readonly int _glTexHandle;
        private State _state;

        public CudaGlInteropBuffer(int width, int height, CudaAccelerator accelerator)
            : base(accelerator, width * height * sizeof(float), sizeof(float))
        {
            Size = new Vector2i(width, height);

            // Create the OpenGL buffers
            _glPboHandle = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, _glPboHandle);
            GL.BufferData(
                BufferTarget.PixelUnpackBuffer,
                width * height * sizeof(float),
                IntPtr.Zero,
                BufferUsageHint.DynamicCopy);
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0);

            _glTexHandle = GL.GenTexture();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _glTexHandle);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int) TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int) TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,
                (int) TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,
                (int) TextureWrapMode.ClampToBorder);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            // Register the resource with CUDA
            CudaException.ThrowIfFailed(CudaGlInterop.RegisterBuffer(
                out _cudaResource,
                _glPboHandle,
                (int) CudaGraphicsMapFlags.None)); // None => Cuda can both read and write the buffer

            _state = State.AvailableForGl;
        }

        public void BindGlTexture(TextureUnit unit)
        {
            if (_state == State.MappedToCuda)
                throw new Exception(
                    "Cannot bind interop buffer to OpenGL texture, as it is still mapped ot CUDA memory");

            GL.ActiveTexture(unit);
        }

        public ArrayView2D<float, Stride2D.DenseX> MapCuda(CudaStream stream)
        {
            if (_state == State.AvailableForGl)
            {
                unsafe
                {
                    fixed (IntPtr* pResources = &_cudaResource)
                    {
                        CudaException.ThrowIfFailed(CudaGlInterop.MapResources(
                            1, new IntPtr(pResources), stream.StreamPtr));
                        _state = State.MappedToCuda;
                    }
                }

                CudaException.ThrowIfFailed(CudaGlInterop.GetMappedPointer(
                    out var devicePtr, out var bufLen, _cudaResource));
                Trace.Assert(bufLen == Size.X * Size.Y * sizeof(float));
                NativePtr = devicePtr;

                _state = State.MappedToCuda;
            }

            Trace.Assert(NativePtr != IntPtr.Zero);

            var viewBase = AsArrayView<float>(0, Length);
            var view2d = ((ArrayView1D<float, Stride1D.Dense>) viewBase)
                .As2DDenseXView((Size.X, Size.Y));

            return view2d;
        }

        public void UnmapCuda(CudaStream stream)
        {
            if (_state == State.MappedToCuda)
            {
                unsafe
                {
                    fixed (IntPtr* pResources = &_cudaResource)
                    {
                        CudaException.ThrowIfFailed(CudaGlInterop.UnmapResources(
                            1, new IntPtr(pResources), stream.StreamPtr));
                        NativePtr = IntPtr.Zero;
                        _state = State.AvailableForGl;
                    }
                }

                GL.BindBuffer(BufferTarget.PixelUnpackBuffer, _glPboHandle);
                GL.BindTexture(TextureTarget.Texture2D, _glTexHandle);

                // TODO: TexImage2D will allocate a new texture.
                // Modify this to allocate a texture in the constructor, then copy into with TexSubImage2D
                GL.TexImage2D(TextureTarget.Texture2D,
                    0,
                    PixelInternalFormat.R32f,
                    Size.X,
                    Size.Y,
                    0,
                    PixelFormat.Red,
                    PixelType.Float,
                    IntPtr.Zero);
            }
        }

        public static void CudaMemSet<T>(CudaStream stream, byte value, in ArrayView<T> targetView)
            where T : unmanaged
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            if (targetView.GetAcceleratorType() != AcceleratorType.Cuda) throw new NotSupportedException();

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
                throw new NotSupportedException();

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


        protected override void MemSet(
            AcceleratorStream stream,
            byte value,
            in ArrayView<byte> targetView)
        {
            CudaMemSet(stream as CudaStream, value, targetView);
        }

        protected override void CopyFrom(
            AcceleratorStream stream,
            in ArrayView<byte> sourceView,
            in ArrayView<byte> targetView)
        {
            CudaCopy(stream as CudaStream, sourceView, targetView);
        }

        protected override void CopyTo(
            AcceleratorStream stream,
            in ArrayView<byte> sourceView,
            in ArrayView<byte> targetView)
        {
            CudaCopy(stream as CudaStream, sourceView, targetView);
        }

        protected override void DisposeAcceleratorObject(bool disposing)
        {
            // Unregister the CUDA resource
            // Delete the gl texture
            // Delete the gl pbo
            throw new NotImplementedException();
        }

        private enum State
        {
            AvailableForGl,
            MappedToCuda
        }
    }
}