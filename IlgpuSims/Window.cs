using System.Diagnostics;
using ILGPU.Runtime.Cuda;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Platform.Windows;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

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
        }
        
        static byte[] MakeDummyImage(int width, int height)
        {
            var data = new byte[width * height * 3];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    data[(x + y * width) * 3 + 0] = (byte) (255.0 * (float) x / (float) width);
                    data[(x + y * width) * 3 + 1] = (byte) (255.0 * (float) y / (float) height);
                    data[(x + y * width) * 3 + 2] = 0;
                }
            }

            return data;
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            
            GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
            
            _vertexBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);

            _vertexArrayObject = GL.GenVertexArray();
            GL.BindVertexArray(_vertexArrayObject);
            
            _shader = ShaderModule.FromPaths("./shader.vert", "./shader.frag");
            _shader.Use();

            var vertexLocation = _shader.GetAttribLocation("aPosition");
            GL.EnableVertexAttribArray(vertexLocation);
            GL.VertexAttribPointer(vertexLocation, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);

            
            var imageData = MakeDummyImage(Size.X, Size.Y);
            _image = Texture.FromRawRgb(imageData, Size.X, Size.Y);
            _image.Use(TextureUnit.Texture0);

            _cudaGlMemoryBuffer = new CudaGlMemoryBuffer(_accelerator, _image);
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            if (KeyboardState.IsKeyDown(Keys.Escape))
            {
                Close();
            }

            _cudaGlMemoryBuffer.Map(_stream);
            
            _cudaGlMemoryBuffer.Unmap(_stream);
            
            base.OnUpdateFrame(args);
        }
        
        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);
            
            GL.Clear(ClearBufferMask.ColorBufferBit);
            
            GL.BindVertexArray(_vertexArrayObject);
            _image.Use(TextureUnit.Texture0);
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
        private Texture _image;
        private CudaGlMemoryBuffer _cudaGlMemoryBuffer;

        private readonly float[] _vertices = {
            -1.0f,  1.0f, 0.0f,  // top left
            -1.0f, -1.0f, 0.0f,  // bottom left
             1.0f,  1.0f, 0.0f,  // top right
             1.0f, -1.0f, 0.0f,  // bottom right
        };
    }
}