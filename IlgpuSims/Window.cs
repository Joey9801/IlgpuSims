using System;
using System.Diagnostics;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Platform.Windows;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using static ILGPU.Runtime.Cuda.CudaAPI;

namespace IlgpuSims
{
    public class Window : GameWindow
    {
        public Window(int width, int height, CudaAccelerator accelerator)
            : base(GameWindowSettings.Default, new NativeWindowSettings { WindowBorder = WindowBorder.Fixed })
        {
            _accelerator = accelerator;
            _stream = _accelerator.CreateStream() as CudaStream;
            Trace.Assert(_stream != null);
            Size = new Vector2i(width, height);
            Title = "ILGPU/OpenGL interop test";
        }

        static void MakeDummyImageKernel(Index2D idx, ArrayView2D<float, Stride2D.DenseX> arr)
        {
            var x = 2 * 16 * (float) idx.X / arr.Extent.X;
            var y = 2 * 9 * (float) idx.Y / arr.Extent.Y;

            arr[idx.X, idx.Y] = (MathF.Sin(x) + MathF.Cos(y) + 2) / 4;
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            
            GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
            
            _shader = ShaderModule.FromPaths("./shader.vert", "./shader.frag");
            _shader.Use();
            
            _vertexBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);
            
            _vertexArrayObject = GL.GenVertexArray();
            GL.BindVertexArray(_vertexArrayObject);
            
            var vertexLocation = _shader.GetAttribLocation("aPosition");
            GL.EnableVertexAttribArray(vertexLocation);
            GL.VertexAttribPointer(vertexLocation, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            
            // Create the PBO
            _pboHandle = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, _pboHandle);
            
            GL.BufferData(
                BufferTarget.PixelUnpackBuffer,
                Size.X * Size.Y * sizeof(float),
                IntPtr.Zero,
                BufferUsageHint.DynamicDraw);

            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0);
            
            _texture = GL.GenTexture();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _texture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                (int) TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                (int) TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,
                (int) TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,
                (int) TextureWrapMode.ClampToBorder);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            

            // Map the PBO into CUDA memory, and use ILGPU to fill it with data
            CudaException.ThrowIfFailed(CudaGlInterop.RegisterBuffer(
                    out _cudaResource,
                    _pboHandle,
                    (int) CudaGraphicsMapFlags.WriteDiscard));
            unsafe
            {
                fixed (IntPtr* pResources = &_cudaResource)
                {
                    var resources = new IntPtr(pResources);
                    
                    CudaException.ThrowIfFailed(CudaGlInterop.MapResources(
                        1, resources, _stream.StreamPtr));

                    CudaException.ThrowIfFailed(CudaGlInterop.GetMappedPointer(
                        out var devicePtr, out var size, _cudaResource));

                    using var buff = new CudaGlInteropBuffer(_stream.Accelerator, devicePtr, size);
                    var baseView = buff.AsArrayView<byte>(0, Size.X * Size.Y * sizeof(float)).Cast<float>();
                    var view2d = ((ArrayView1D<float, Stride1D.Dense>) baseView).As2DView(
                        new LongIndex2D(Size.X, Size.Y),
                        new Stride2D.DenseX(Size.X));
                    _stream.Accelerator.LaunchAutoGrouped(MakeDummyImageKernel, _stream, new Index2D(Size.X, Size.Y), view2d);
                    _stream.Synchronize();
                    
                    
                    CudaException.ThrowIfFailed(CudaGlInterop.UnmapResources(
                        1, resources, _stream.StreamPtr));
                }
            }
            
            // Create a texture from the PBO contents
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, _pboHandle);
            GL.BindTexture(TextureTarget.Texture2D, _texture);
            GL.TexImage2D(TextureTarget.Texture2D,
                0,
                PixelInternalFormat.R32f,
                Size.X,
                Size.Y,
                0,
                PixelFormat.Red,
                PixelType.Float,
                IntPtr.Zero);
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            if (KeyboardState.IsKeyDown(Keys.Escape))
            {
                Close();
            }

        }
        
        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);
            
            GL.Clear(ClearBufferMask.ColorBufferBit);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _texture);
            _shader.Use();
            
            GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
            
            Context.SwapBuffers();
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            
            GL.Viewport(0, 0, Size.X, Size.Y);
        }

        private readonly CudaAccelerator _accelerator;
        private readonly CudaStream _stream;
        
        private int _vertexBufferObject;
        private int _vertexArrayObject;
        
        private ShaderModule _shader;

        private int _pboHandle;
        private int _texture;
        private IntPtr _cudaResource;

        private readonly float[] _vertices = {
            -1.0f,  1.0f, 0.0f,  // top left
            -1.0f, -1.0f, 0.0f,  // bottom left
             1.0f,  1.0f, 0.0f,  // top right
             1.0f, -1.0f, 0.0f,  // bottom right
        };

    }
}