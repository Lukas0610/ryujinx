using System;

namespace Ryujinx.Graphics.GAL
{
    public interface IWindow
    {
        int Width { get; }
        int Height { get; }

        void Present(ITexture texture, ImageCrop crop, Action swapBuffersCallback);

        void SetSize(int width, int height);

        void ChangeVSyncMode(bool vsyncEnabled);

        void SetAntiAliasing(AntiAliasing antialiasing);
        void SetScalingFilter(ScalingFilter type);
        void SetScalingFilterLevel(float level);
        void SetColorSpacePassthrough(bool colorSpacePassThroughEnabled);
    }
}
