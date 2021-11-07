using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace IlgpuSims
{
    class Program
    {
        static void Main(string[] args)
        {
            const int height = 600;
            const int width = 800;

            var ilgpuContext = Context.Create()
                .Debug()
                .Cuda()
                .Optimize(OptimizationLevel.Debug)
                .ToContext();
            var accelerator = ilgpuContext.CreateCudaAccelerator(0);
            var a = accelerator.Allocate1D<float>(1);
            
            using var window = new Window(width, height, accelerator);
            window.Run();
        }
    }
}