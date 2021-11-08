using System;
using System.Diagnostics;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace IlgpuSims
{
    public sealed class Window : GameWindow
    {
        public Window(int width, int height, CudaAccelerator accelerator)
            : base(GameWindowSettings.Default, new NativeWindowSettings {WindowBorder = WindowBorder.Fixed})
        {
            Size = new Vector2i(width, height);
            Title = "ILGPU/OpenGL interop test";

            _accelerator = accelerator;
            _stream = _accelerator.CreateStream() as CudaStream;
            Trace.Assert(_stream != null);

            _interopBuff = new CudaGlInteropBuffer(width, height, _accelerator);
        }

        // Generate some nonsense to confirm that everything is working
        static void MakeDummyImageKernel(Index2D idx, ArrayView2D<float, Stride2D.DenseX> arr, float t)
        {
            var x = 2 * 16 * (float) idx.X / arr.Extent.X * MathF.Cos(t * 1.5f);
            var y = 2 * 9 * (float) idx.Y / arr.Extent.Y * MathF.Sin(t);

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
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices,
                BufferUsageHint.StaticDraw);

            _vertexArrayObject = GL.GenVertexArray();
            GL.BindVertexArray(_vertexArrayObject);

            var vertexLocation = _shader.GetAttribLocation("aPosition");
            GL.EnableVertexAttribArray(vertexLocation);
            GL.VertexAttribPointer(vertexLocation, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            if (KeyboardState.IsKeyDown(Keys.Escape))
            {
                Close();
            }

            _totalTime += args.Time;
            var interopView = _interopBuff.MapCuda(_stream);
            _accelerator.LaunchAutoGrouped(MakeDummyImageKernel, _stream, new Index2D(Size.X, Size.Y), interopView,
                (float) _totalTime);
            _interopBuff.UnmapCuda(_stream);
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            GL.Clear(ClearBufferMask.ColorBufferBit);

            _interopBuff.BindGlTexture(TextureUnit.Texture0);
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
        private readonly CudaGlInteropBuffer _interopBuff;
        private double _totalTime = 0;


        private readonly float[] _vertices = {
            -1.0f,  1.0f, 0.0f,  // top left
            -1.0f, -1.0f, 0.0f,  // bottom left
             1.0f,  1.0f, 0.0f,  // top right
             1.0f, -1.0f, 0.0f,  // bottom right
        };
    }
}