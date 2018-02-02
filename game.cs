using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Cloo;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;

namespace Template {

    class Game
    {
        // screen surface to draw to
        public Surface screen;
        public bool GLinterop = true;
        float zoom = 1, scrollValue = 0, oldValue = 0;

        // load the OpenCL program; this creates the OpenCL context
        static OpenCLProgram ocl = new OpenCLProgram("../../kernel.cl");
        // find the kernel named 'device_function' in the program
        OpenCLKernel kernel = new OpenCLKernel(ocl, "device_function");
        OpenCLKernel secondKernel = new OpenCLKernel(ocl, "refresh_arrays");
        OpenCLKernel imageClearKernel = new OpenCLKernel(ocl, "clear_image");
        OpenCLImage<int> image = new OpenCLImage<int>(ocl, 512, 512);

        // create a regular buffer; by default this resides on both the host and the device
        OpenCLBuffer<int> buffer;
        // create an OpenGL texture to which OpenCL can send data
        //OpenCLImage<int> image = new OpenCLImage<int>(ocl, 512, 512);

        OpenCLBuffer<uint> pattern;
        OpenCLBuffer<uint> second;

        // stopwatch
        Stopwatch timer = new Stopwatch();
        int generation = 0;
        // two buffers for the pattern: simulate reads 'second', writes to 'pattern'
        //uint[] pattern;
        //uint[] second;
        uint pw, ph; // note: pw is in uints; width in bits is 32 this value.
                     // helper function for setting one bit in the pattern buffer

        int resolution = 512;
        int oldResolution = 512;

        void BitSet(uint x, uint y)
        {
            pattern[y * pw + (x >> 5)] |= 1U << (int)(x & 31);
        }
        // helper function for getting one bit from the secondary pattern buffer
        uint GetBit(uint x, uint y)
        {
            /*uint temp = x >> 5;
            temp += (y * pw);
            temp = second[temp];
            int temp2 = (int)(x & 31);
            temp >>= temp2;
            temp &= 1U;*/
            return (second[y * pw + (x >> 5)] >> (int)(x & 31)) & 1U;
        }

        // mouse handling: dragging functionality
        uint xoffset = 0, yoffset = 0;
        bool lastLButtonState = false;
        int dragXStart, dragYStart, offsetXStart, offsetYStart;
        public void SetMouseState(int x, int y, bool pressed)
        {
            if (pressed)
            {
                if (lastLButtonState)
                {
                    int deltax = x - dragXStart, deltay = y - dragYStart;
                    xoffset = (uint)Math.Min(pw * 32 - screen.width, Math.Max(0, offsetXStart - deltax));
                    yoffset = (uint)Math.Min(ph - screen.height, Math.Max(0, offsetYStart - deltay));
                }
                else
                {
                    dragXStart = x;
                    dragYStart = y;
                    offsetXStart = (int)xoffset;
                    offsetYStart = (int)yoffset;
                    lastLButtonState = true;
                }
            }
            else lastLButtonState = false;
        }

        public void SetZoom(float mouseWheel)
        {
            scrollValue += oldValue - mouseWheel;
            oldValue = mouseWheel;
            if (scrollValue < -3) scrollValue = -3;
            else if (scrollValue > 24) scrollValue = 24;
            zoom = 1 + scrollValue * 0.25f;
        }
        // minimalistic .rle file reader for Golly files (see http://golly.sourceforge.net)
        public void Init()
        {
            StreamReader sr = new StreamReader("../../data/turing_js_r.rle");
            uint state = 0, n = 0, x = 1, y = 1;
            while (true)
            {
                String line = sr.ReadLine();
                if (line == null) break; // end of file
                int pos = 0;
                if (line[pos] == '#') continue; /* comment line */
                else if (line[pos] == 'x') // header
                {
                    String[] sub = line.Split(new char[] { '=', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    pw = ((UInt32.Parse(sub[1]) + 33) / 32);
                    ph = UInt32.Parse(sub[3]) + 2;
                    pattern = new OpenCLBuffer<uint>(ocl, (pw * ph));
                    second = new OpenCLBuffer<uint>(ocl, (pw * ph));
                    buffer = new OpenCLBuffer<int>(ocl, 512 * 512);
                    kernel.SetArgument(3, pw);
                    kernel.SetArgument(4, ph);
                }
                else while (pos < line.Length)
                    {
                        Char c = line[pos++];
                        if (state == 0) if (c < '0' || c > '9') { state = 1; n = Math.Max(n, 1); } else n = (uint)(n * 10 + (c - '0'));
                        if (state == 1) // expect other character
                        {
                            if (c == '$') { y += n; x = 1; } // newline
                            else if (c == 'o') for (int i = 0; i < n; i++) BitSet(x++, y); else if (c == 'b') x += n;
                            state = n = 0;
                        }
                    }
            }
            // swap buffers
            for (int i = 0; i < pw * ph; i++) second[i] = pattern[i];
            kernel.SetArgument(1, pattern);
            kernel.SetArgument(2, second);
            secondKernel.SetArgument(0, pattern);
            secondKernel.SetArgument(1, second);
            second.CopyToDevice();
        }
        // SIMULATE
        // Takes the pattern in array 'second', and applies the rules of Game of Life to produce the next state
        // in array 'pattern'. At the end, the result is copied back to 'second' for the next generation.
        void Simulate()
        {
            // clear destination pattern
            for (int i = 0; i < pw * ph; i++) pattern[i] = 0;
            // process all pixels, skipping one pixel boundary
            uint w = pw * 32, h = ph;
            for (uint y = 1; y < h - 1; y++) for (uint x = 1; x < w - 1; x++)
                {
                    // count active neighbors
                    uint n = GetBit(x - 1, y - 1) + GetBit(x, y - 1) + GetBit(x + 1, y - 1) + GetBit(x - 1, y) +
                             GetBit(x + 1, y) + GetBit(x - 1, y + 1) + GetBit(x, y + 1) + GetBit(x + 1, y + 1);
                    if ((GetBit(x, y) == 1 && n == 2) || n == 3) BitSet(x, y);
                }
            // swap buffers
            for (int i = 0; i < pw * ph; i++) second[i] = pattern[i];
        }
        // TICK
        // Main application entry point: the template calls this function once per frame.
        public void Tick()
        {
            GL.Finish();
            // start timer
            timer.Restart();
            //Initiate work sizes
            long[] workSize = { pw, ph };
            long[] workSize2 = { pw * ph };
            //Set kernel arguments
            kernel.SetArgument(5, xoffset);
            kernel.SetArgument(6, yoffset);
            resolution = (int)(zoom * 512);
            long[] workSize3 = { resolution / 32, resolution };
            kernel.SetArgument(7, resolution);
            // run the simulation, 1 step
            screen.Clear(0);
            if (GLinterop)
            {
                if(resolution != oldResolution)
                {
                    image = new OpenCLImage<int>(ocl, resolution, resolution);
                    oldResolution = resolution;
                }

                //set image as argument
                imageClearKernel.SetArgument(0, image);

                imageClearKernel.LockOpenGLObject(image.texBuffer);
                imageClearKernel.Execute(workSize3);
                imageClearKernel.UnlockOpenGLObject(image.texBuffer);

                //lock image object
                kernel.SetArgument(0, image);
                kernel.LockOpenGLObject(image.texBuffer);

                //run kernel
                kernel.Execute(workSize);

                //unlock image object
                kernel.UnlockOpenGLObject(image.texBuffer);

                secondKernel.Execute(workSize2);

            }
            else
            {
                kernel.SetArgument(0, buffer);
                for (int i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = 0;
                }
                buffer.CopyToDevice();
                kernel.Execute(workSize);
                secondKernel.Execute(workSize2);
                buffer.CopyFromDevice();
                for (uint y = 0; y < screen.height; y++) for (uint x = 0; x < screen.width; x++)
                    {
                        screen.Plot(x, y, buffer[x + y * 512]);
                    }
            }
            // visualize current state
            // report performance
            Console.WriteLine("generation " + generation++ + ": " + timer.ElapsedMilliseconds + "ms");
            //Console.ReadLine();
        }

        public void Render()
        {
            // use OpenGL to draw a quad using the texture that was filled by OpenCL
            if (GLinterop)
            {
                GL.LoadIdentity();
                GL.BindTexture(TextureTarget.Texture2D, image.OpenGLTextureID);
                GL.Begin(PrimitiveType.Quads);
                GL.TexCoord2(0.0f, 1.0f); GL.Vertex2(-1.0f, -1.0f);
                GL.TexCoord2(1.0f, 1.0f); GL.Vertex2(1.0f, -1.0f);
                GL.TexCoord2(1.0f, 0.0f); GL.Vertex2(1.0f, 1.0f);
                GL.TexCoord2(0.0f, 0.0f); GL.Vertex2(-1.0f, 1.0f);
                GL.End();
            }
        }
    }

} // namespace Template
