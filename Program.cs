using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace MarkovImage
{
    internal class Program
    {
        private static Dictionary<Color, List<Color>> markov;
        private static byte[] bytes;
        private static int pixelSize;
        private static int stride;
        private static int width;
        private static int height;
        private static Random random;
        private static Stopwatch stopwatch;
        private static int pointsMin;
        private static int pointsMax;

        static void Main(string[] args)
        {
            Console.Title = "Art from Probability";
            stopwatch = new Stopwatch();
            random = new Random();

            markov = new Dictionary<Color, List<Color>>();

            Console.WriteLine("Enter the source file:");
            string source = Console.ReadLine();

            Console.WriteLine("Enter the output file:");
            string output = Console.ReadLine();

            Console.WriteLine("Enter the smallest number of starting points:");
            string result = Console.ReadLine();
            if(!int.TryParse(result, out pointsMin))
            {
                System.Environment.Exit(13);
            }
            
            Console.WriteLine("Enter the largest number of starting points:");
            result = Console.ReadLine();
            if(!int.TryParse(result, out pointsMax))
            {
                System.Environment.Exit(13);
            }

            Bitmap bmp = new Bitmap(source);

            // Lock the bitmap's bits.  
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            System.Drawing.Imaging.BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadWrite, bmp.PixelFormat);

            pixelSize = data.PixelFormat == PixelFormat.Format32bppArgb ? 4 : 3; // only works with 32 or 24 pixel-size bitmap!
            var padding = data.Stride - (data.Width * pixelSize);
            stride = data.Stride;
            bytes = new byte[data.Height * data.Stride];

            height = data.Height;
            width = data.Width;

            Console.WriteLine($"{data.PixelFormat} {data.Scan0} {padding} {bytes.Length} {data.Stride} {pixelSize} {data.Width} {data.Height}");

            // Copy the RGB values into the array.
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

            //Markov
            stopwatch.Restart();
            try
            {
                for (int y = 0; y < data.Height; y++)
                {
                    for (int x = 0; x < data.Width; x++)
                    {
                        Color colour;
                        Color neighbour;
                        GetPixel(ref bytes, ref pixelSize, GetIndex(x, y, ref stride, ref pixelSize), out colour);

                        if(!markov.ContainsKey(colour))
                        {
                            markov.Add(colour,new List<Color>());
                        }

                        if(x > 0)
                        {
                            GetPixel(ref bytes, ref pixelSize, GetIndex(x - 1, y, ref stride, ref pixelSize), out neighbour);
                            markov[colour].Add(neighbour);
                        }

                        if(x < data.Width - 1)
                        {
                            GetPixel(ref bytes, ref pixelSize, GetIndex(x + 1, y, ref stride, ref pixelSize), out neighbour);
                            markov[colour].Add(neighbour);
                        }

                        if(y > 0)
                        {
                            GetPixel(ref bytes, ref pixelSize, GetIndex(x, y - 1, ref stride, ref pixelSize), out neighbour);
                            markov[colour].Add(neighbour);
                        }

                        if(y < data.Height - 1)
                        {
                            GetPixel(ref bytes, ref pixelSize, GetIndex(x, y + 1, ref stride, ref pixelSize), out neighbour);
                            markov[colour].Add(neighbour);
                        }
                    }
                }
            } catch(System.Exception e)
            {
                Console.WriteLine($"Error when generating Markov chains: {e}");
            }

            stopwatch.Stop();
            Console.WriteLine("Markov chains generated in {0}", stopwatch.Elapsed);

            for (int y = 0; y < data.Height; y++)
            {
                for (int x = 0; x < data.Width; x++)
                {
                    SetPixel(ref bytes, ref pixelSize, GetIndex(x, y, ref stride, ref pixelSize), Color.Transparent);
                }
            }

            stopwatch.Restart();
            int startPoints = random.Next(pointsMin, pointsMax+1);
            Console.WriteLine("No start points: {0}", startPoints);
            RandomWalk(startPoints);
            stopwatch.Stop();
            Console.WriteLine("Image generated in {0}", stopwatch.Elapsed);

            // Copy the RGB values back to the bitmap
            Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);

            bmp.Save(output);

            // Unlock the bits.
            bmp.UnlockBits(data);

            bmp.Dispose();
            Console.WriteLine("Press any key to exit...");
            Console.ReadLine();
        }

        private static void RandomWalk(int numStartPoints)
        {
            int pixels = 0;
            bool[,] visited = new bool[width,height];
            List<FloodFillObject> queue = new List<FloodFillObject>(width * height);

            Color[] markovKeys = markov.Keys.ToArray();

            Color currentColour;

            for (int i = 0; i < numStartPoints; i++)
            {
                currentColour = markovKeys[random.Next(0, markovKeys.Length)];
                queue.Add(new FloodFillObject(new Point(random.Next(0,width-1),random.Next(0,height-1)), currentColour));
            }

            while(queue.Count > 0)
            {
                int index = random.Next(0, queue.Count);
                var n = queue[index];
                queue.RemoveAt(index);

                if(visited[n.Point.X,n.Point.Y])
                    continue;

                if(n.Point.X < 0 || n.Point.Y < 0 || n.Point.X > width - 1 || n.Point.Y > height - 1)
                    continue;

                visited[n.Point.X,n.Point.Y] = true;
                pixels++;

                if(pixels > (width * height))
                    break;

                Console.Title = $"{(width * height) - pixels}";

                currentColour = markov[n.PreviousColour][random.Next(0, markov[n.PreviousColour].Count)];
                SetPixel(ref bytes, ref pixelSize, GetIndex(n.Point.X, n.Point.Y, ref stride, ref pixelSize), currentColour);

                if(InRange(n.Point.X + 1, n.Point.Y) && !visited[n.Point.X + 1, n.Point.Y])
                    queue.Add(new FloodFillObject(new Point(n.Point.X + 1, n.Point.Y), currentColour));

                if(InRange(n.Point.X - 1, n.Point.Y) && !visited[n.Point.X - 1, n.Point.Y])
                    queue.Add(new FloodFillObject(new Point(n.Point.X - 1, n.Point.Y), currentColour));

                if(InRange(n.Point.X, n.Point.Y + 1) && !visited[n.Point.X, n.Point.Y + 1])
                    queue.Add(new FloodFillObject(new Point(n.Point.X, n.Point.Y + 1), currentColour));

                if(InRange(n.Point.X, n.Point.Y - 1) && !visited[n.Point.X, n.Point.Y - 1])
                    queue.Add(new FloodFillObject(new Point(n.Point.X, n.Point.Y - 1), currentColour));
            }

            //Debugging
            //Console.WriteLine("Remaining: {0}", (width * height) - pixels);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool InRange(int x, int y)
        {
            return x >= 0 && x < width && y >= 0 && y < height;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetIndex(int x, int y, ref int stride, ref int pixelSize)
        {
            return (y * stride) + (x * pixelSize);
        }

        private static void GetPixel(ref byte[] data, ref int pixelSize, int pixelIndex, out Color color)
        {
            color = Color.FromArgb(pixelSize == 3 ? 255 : data[pixelIndex + 3], data[pixelIndex + 2], data[pixelIndex + 1], data[pixelIndex]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetPixel(ref byte[] data, ref int pixelSize, int pixelIndex, Color color)
        {
            SetPixel(ref data, ref pixelSize, pixelIndex, color.R, color.G, color.B, color.A);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetPixel(ref byte[] data, ref int pixelSize, int pixelIndex, byte r, byte g, byte b, byte a = 255)
        {
            if(pixelSize > 3)
                data[pixelIndex + 3] = a;
            
            data[pixelIndex + 2] = r;
            data[pixelIndex + 1] = g;
            data[pixelIndex] = b;
        }
    }
}