using System.Runtime.InteropServices;

namespace TinyRaycaster;

public static class SDL
{
    // SDL2 P/Invoke definitions
    
    const string libName = "SDL2";

    public const uint SDL_INIT_VIDEO = 0x00000020u;

    [Flags]
    public enum SDL_WindowFlags
    {
        SDL_WINDOW_SHOWN = 0x00000004,
        SDL_WINDOW_INPUT_FOCUS = 0x00000200
    }

    public const int SDL_TEXTUREACCESS_STREAMING = 1;
    public const uint SDL_PIXELFORMAT_ARGB8888 = 372645892u;

    public const uint SDL_KEYDOWN = 0x300u;
    public const uint SDL_KEYUP = 769u;
    public const uint SDLK_ESCAPE = 27u;

    [StructLayout(LayoutKind.Sequential)]
    public struct SDL_Keysym
    {
        public SDL_Scancode scancode;
        public uint sym;
        public SDL_Keymod mod;
        public uint unicode;
    }

    public enum SDL_Scancode
    {
        SDL_SCANCODE_ESCAPE = 41
    }

    public enum SDL_Keymod
    {
        KMOD_NONE = 0x0000
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SDL_KeyboardEvent
    {
        public uint type;
        public uint timestamp;
        public uint windowID;
        public byte state;
        public byte repeat;
        byte padding2;
        byte padding3;
        public SDL_Keysym keysym;
    }

    [DllImport(libName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_Init(uint flags);

    [DllImport(libName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_CreateWindowAndRenderer(int width, int height, SDL_WindowFlags flags, ref IntPtr window, ref IntPtr renderer);

    [DllImport(libName, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint SDL_GetTicks();

    [DllImport(libName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_PollEvent(ref SDL_KeyboardEvent _event);

    [DllImport(libName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr SDL_CreateTexture(IntPtr renderer, uint format, int access, int width, int height);

    [DllImport(libName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_UpdateTexture(IntPtr texture, IntPtr rect, IntPtr pixels, int pitch);

    [DllImport(libName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_RenderClear(IntPtr renderer);

    [DllImport(libName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_RenderCopy(IntPtr renderer, IntPtr texture, IntPtr srcrect, IntPtr destrect);

    [DllImport(libName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_RenderPresent(IntPtr renderer);

    [DllImport(libName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_DestroyTexture(IntPtr texture);

    [DllImport(libName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_DestroyRenderer(IntPtr renderer);

    [DllImport(libName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_DestroyWindow(IntPtr window);

    [DllImport(libName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_Quit();
}