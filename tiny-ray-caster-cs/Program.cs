using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;

namespace TinyRaycaster
{

    // Pressed state struct
    public struct Pressed
    {
        public bool left;
        public bool right;
        public bool forward;
        public bool backward;

        public (double turn, double walk) ToTurnWalk()
        {
            double turn = (left && !right) ? -1.0 : (right && !left) ? 1.0 : 0.0;
            double walk = (forward && !backward) ? 1.0 : (backward && !forward) ? -1.0 : 0.0;
            return (turn, walk);
        }
    }

    internal class Program
    {
        static char[,] map;
        static int viewWidth = 1024;
        static int viewHeight = 512;
        static int mapw, maph;
        static int tilew, tileh;
        static double fov = Math.PI / 3.0;
        static double drawAngleSlice;
        static double turnSpeed = 0.01;
        static double walkSpeed = 0.02;

        static uint white;
        static uint ceilingColour;
        static uint floorColour;
        static uint black;

        static double[] rayList;
        static int[] viewSliceList;

        static uint[][] wallRows;

        static uint[] frameBuffer;
        static IntPtr bufferPtr;

        static void Main(string[] args)
        {
            Initialize();

            SDL.SDL_Init(SDL.SDL_INIT_VIDEO);

            IntPtr window = IntPtr.Zero;
            IntPtr renderer = IntPtr.Zero;
            var windowFlags = SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN | SDL.SDL_WindowFlags.SDL_WINDOW_INPUT_FOCUS;
            SDL.SDL_CreateWindowAndRenderer(viewWidth, viewHeight, windowFlags, ref window, ref renderer);

            IntPtr texture = SDL.SDL_CreateTexture(renderer, SDL.SDL_PIXELFORMAT_ARGB8888, 
                SDL.SDL_TEXTUREACCESS_STREAMING, viewWidth, viewHeight);

            frameBuffer = new uint[viewWidth * viewHeight];
            for (int i = 0; i < frameBuffer.Length; i++)
                frameBuffer[i] = black;

            // Pin the array and get pointer
            GCHandle handle = GCHandle.Alloc(frameBuffer, GCHandleType.Pinned);
            bufferPtr = handle.AddrOfPinnedObject();

            var keyEvent = new SDL.SDL_KeyboardEvent();

            wallRows = LoadWallRows("walltext.png");

            uint lastTicks = SDL.SDL_GetTicks();

            double px = 3.456, py = 2.345, pa = 0.0;
            var pressed = new Pressed { left = false, right = false, forward = false, backward = false };

            DrawLoop(px, py, pa, pressed, renderer, texture, ref keyEvent);

            // Cleanup
            handle.Free();
            SDL.SDL_DestroyTexture(texture);
            SDL.SDL_DestroyRenderer(renderer);
            SDL.SDL_DestroyWindow(window);
            SDL.SDL_Quit();
        }

        static void Initialize()
        {
            // Load map
            var mapLines = File.ReadAllLines("map.txt");
            map = new char[mapLines.Length, mapLines[0].Length];
            for (int y = 0; y < mapLines.Length; y++)
            {
                for (int x = 0; x < mapLines[y].Length; x++)
                {
                    map[y, x] = mapLines[y][x];
                }
            }

            mapw = map.GetLength(0);
            maph = map.GetLength(1);
            tilew = viewWidth / 2 / mapw;
            tileh = viewHeight / maph;
            drawAngleSlice = fov / (viewWidth / 2.0);

            // Initialize colors
            white = AsUint32(255, 255, 255);
            ceilingColour = AsUint32(200, 200, 200);
            floorColour = AsUint32(150, 150, 150);
            black = AsUint32(0, 0, 0);

            // Initialize lists
            rayList = new double[201]; // 0.0 to 20.0 step 0.1
            for (int i = 0; i <= 200; i++)
                rayList[i] = i * 0.1;

            viewSliceList = new int[viewWidth / 2];
            for (int i = 0; i < viewSliceList.Length; i++)
                viewSliceList[i] = i;
        }

        static uint AsUint32(byte r, byte g, byte b)
        {
            return (uint)((b) | (g << 8) | (r << 16) | (255 << 24));
        }

        static bool IsOpen(double x, double y)
        {
            int ix = (int)x;
            int iy = (int)y;
            if (ix < 0 || ix >= mapw || iy < 0 || iy >= maph)
                return false;
            return map[ix, iy] == ' ';
        }

        static uint[][] LoadWallRows(string filename)
        {
            using (var image = new Bitmap(filename))
            {
                var result = new uint[image.Width][];
                for (int x = 0; x < image.Width; x++)
                {
                    result[x] = new uint[image.Height];
                    for (int y = 0; y < image.Height; y++)
                    {
                        var pixel = image.GetPixel(x, y);
                        result[x][y] = AsUint32(pixel.R, pixel.G, pixel.B);
                    }
                }
                return result;
            }
        }

        static void DrawRect(int x, int y, int w, int h, uint color, uint[] array)
        {
            for (int dy = y; dy < y + h && dy < viewHeight; dy++)
            {
                int pos = dy * viewWidth + x;
                for (int dx = 0; dx < w && x + dx < viewWidth; dx++)
                {
                    if (pos + dx < array.Length)
                        array[pos + dx] = color;
                }
            }
        }

        static void DrawMap(uint[][] wallRows, uint[] array)
        {
            // Draw white background for map area
            DrawRect(0, 0, viewWidth / 2, viewHeight, white, array);

            for (int y = 0; y < maph; y++)
            {
                for (int x = 0; x < mapw; x++)
                {
                    if (!IsOpen(x, y))
                    {
                        int wallType = map[x, y] - '0';
                        uint wallColour = wallRows[wallType * 64][0];
                        DrawRect(x * tilew, y * tileh, tilew, tileh, wallColour, array);
                    }
                }
            }
        }

        static double Fraction(double f)
        {
            return Math.Abs(f - Math.Truncate(f));
        }

        static (double stopPoint, int wallType, double wallCol)? DrawRay(double px, double py, double pa, uint[] array)
        {
            double cpa = Math.Cos(pa);
            double spa = Math.Sin(pa);

            (double x, double y) Point(double c) => (px + c * cpa, py + c * spa);

            foreach (double c in rayList)
            {
                var (cx, cy) = Point(c);
                if (!IsOpen(cx, cy))
                {
                    int wallType = map[(int)cx, (int)cy] - '0';

                    // Find exact intersection point
                    double exactC = c;
                    for (double dc = c; dc >= c - 1.0; dc -= 0.005)
                    {
                        var (testX, testY) = Point(dc);
                        if (IsOpen(testX, testY))
                        {
                            exactC = dc;
                            break;
                        }
                    }

                    var (exactX, exactY) = Point(exactC);
                    double fcx = Fraction(exactX);
                    double fcy = Fraction(exactY);
                    double ratio = (fcx > 0.01 && fcx < 0.99) ? fcx : fcy;

                    return (c, wallType, ratio);
                }
                else
                {
                    int pixelx = (int)(cx * tilew);
                    int pixely = (int)(cy * tileh);
                    if (pixely * viewWidth + pixelx < array.Length)
                        array[pixely * viewWidth + pixelx] = black;
                }
            }

            return null;
        }

        static void DrawView(double px, double py, double pa, uint[][] wallRows, uint[] array)
        {
            // Draw ceiling
            DrawRect(viewWidth / 2, 0, viewWidth / 2, viewHeight / 2, ceilingColour, array);
            // Draw floor
            DrawRect(viewWidth / 2, viewHeight / 2, viewWidth / 2, viewHeight / 2, floorColour, array);

            // For each slice find the intersected wall with its height and draw it
            foreach (int i in viewSliceList)
            {
                double angle = pa - (fov / 2.0) + (drawAngleSlice * i);
                var rayResult = DrawRay(px, py, angle, array);

                if (rayResult.HasValue)
                {
                    var (stopPoint, wallType, wallCol) = rayResult.Value;
                    double viewPlaneDist = stopPoint * Math.Cos(angle - pa);
                    int columnHeight = (int)(viewHeight / viewPlaneDist);
                    uint[] wallRow = wallRows[wallType * 64 + (int)(wallCol * 64.0)];

                    double wy = 64.0 / columnHeight;
                    int x = (viewWidth / 2) + i;
                    int y = (viewHeight - columnHeight) / 2;

                    for (int dy = Math.Max(0, y); dy < Math.Min(viewHeight, y + columnHeight); dy++)
                    {
                        int pos = dy * viewWidth + x;
                        int pix = (int)(wy * (dy - y));
                        if (pos < array.Length && pix < wallRow.Length)
                            array[pos] = wallRow[pix];
                    }
                }
            }
        }

        static void DrawLoop(double px, double py, double pa, Pressed pressed, 
            IntPtr renderer, IntPtr texture, ref SDL.SDL_KeyboardEvent keyEvent)
        {
            while (true)
            {
                // Clear frame buffer
                for (int i = 0; i < frameBuffer.Length; i++)
                    frameBuffer[i] = black;

                DrawMap(wallRows, frameBuffer);
                DrawView(px, py, pa, wallRows, frameBuffer);

                SDL.SDL_UpdateTexture(texture, IntPtr.Zero, bufferPtr, viewWidth * 4);
                SDL.SDL_RenderClear(renderer);
                SDL.SDL_RenderCopy(renderer, texture, IntPtr.Zero, IntPtr.Zero);
                SDL.SDL_RenderPresent(renderer);

                // Handle movement
                var (turn, walk) = pressed.ToTurnWalk();
                pa += turnSpeed * turn;

                double newPx = px + (Math.Cos(pa) * walkSpeed * walk);
                double newPy = py + (Math.Sin(pa) * walkSpeed * walk);

                if (IsOpen(newPx, newPy))
                {
                    px = newPx;
                    py = newPy;
                }

                // Handle events
                if (SDL.SDL_PollEvent(ref keyEvent) != 0)
                {
                    if (keyEvent.type == SDL.SDL_KEYDOWN || keyEvent.type == SDL.SDL_KEYUP)
                    {
                        if (keyEvent.keysym.sym == SDL.SDLK_ESCAPE)
                            break;

                        switch (keyEvent.keysym.sym)
                        {
                            case (uint)'a':
                                pressed.left = (keyEvent.type == SDL.SDL_KEYDOWN);
                                break;
                            case (uint)'d':
                                pressed.right = (keyEvent.type == SDL.SDL_KEYDOWN);
                                break;
                            case (uint)'w':
                                pressed.forward = (keyEvent.type == SDL.SDL_KEYDOWN);
                                break;
                            case (uint)'s':
                                pressed.backward = (keyEvent.type == SDL.SDL_KEYDOWN);
                                break;
                        }
                    }
                }
            }
        }
    }
}