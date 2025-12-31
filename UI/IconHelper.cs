using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace VoiceR.UI
{
    public static class IconHelper
    {
        private static string? _iconPath = null;

        /// <summary>
        /// Gets the path to the application icon (.ico file).
        /// If the .ico file doesn't exist, it creates one from the PNG.
        /// </summary>
        public static string GetIconPath()
        {
            if (_iconPath != null && File.Exists(_iconPath))
            {
                return _iconPath;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string icoPath = Path.Combine(baseDir, "Assets", "VoiceR.ico");
            string pngPath = Path.Combine(baseDir, "Assets", "VoiceR.png");

            // If .ico already exists, use it
            if (File.Exists(icoPath))
            {
                _iconPath = icoPath;
                return icoPath;
            }

            // If PNG exists, convert it to ICO
            if (File.Exists(pngPath))
            {
                try
                {
                    ConvertPngToIco(pngPath, icoPath);
                    _iconPath = icoPath;
                    return icoPath;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to convert PNG to ICO: {ex.Message}");
                }
            }

            // If all else fails, return empty string
            return string.Empty;
        }

        /// <summary>
        /// Converts a PNG file to ICO format with multiple sizes.
        /// </summary>
        private static void ConvertPngToIco(string pngPath, string icoPath)
        {
            using (var bitmap = new Bitmap(pngPath))
            {
                using (var fileStream = new FileStream(icoPath, FileMode.Create))
                {
                    // Create an ICO with multiple sizes
                    int[] sizes = { 16, 32, 48, 256 };
                    
                    // ICO header
                    fileStream.WriteByte(0); // Reserved
                    fileStream.WriteByte(0); // Reserved
                    fileStream.WriteByte(1); // Type: 1 = ICO
                    fileStream.WriteByte(0); // Reserved
                    fileStream.WriteByte((byte)sizes.Length); // Number of images
                    fileStream.WriteByte(0); // High byte of count

                    // Calculate offsets
                    int headerSize = 6 + (16 * sizes.Length);
                    var imageData = new byte[sizes.Length][];
                    var offsets = new int[sizes.Length];
                    int currentOffset = headerSize;

                    // Prepare image data for each size
                    for (int i = 0; i < sizes.Length; i++)
                    {
                        int size = sizes[i];
                        using (var resized = new Bitmap(bitmap, size, size))
                        {
                            using (var ms = new MemoryStream())
                            {
                                resized.Save(ms, ImageFormat.Png);
                                imageData[i] = ms.ToArray();
                            }
                        }
                        offsets[i] = currentOffset;
                        currentOffset += imageData[i].Length;
                    }

                    // Write directory entries
                    for (int i = 0; i < sizes.Length; i++)
                    {
                        int size = sizes[i];
                        byte sizeValue = size >= 256 ? (byte)0 : (byte)size;
                        
                        fileStream.WriteByte(sizeValue); // Width
                        fileStream.WriteByte(sizeValue); // Height
                        fileStream.WriteByte(0); // Color palette
                        fileStream.WriteByte(0); // Reserved
                        fileStream.WriteByte(1); // Color planes
                        fileStream.WriteByte(0); // High byte
                        fileStream.WriteByte(32); // Bits per pixel
                        fileStream.WriteByte(0); // High byte
                        
                        // Size of image data
                        byte[] sizeBytes = BitConverter.GetBytes(imageData[i].Length);
                        fileStream.Write(sizeBytes, 0, 4);
                        
                        // Offset to image data
                        byte[] offsetBytes = BitConverter.GetBytes(offsets[i]);
                        fileStream.Write(offsetBytes, 0, 4);
                    }

                    // Write image data
                    foreach (var data in imageData)
                    {
                        fileStream.Write(data, 0, data.Length);
                    }
                }
            }
        }

        /// <summary>
        /// Sets the icon for a WinUI 3 window.
        /// </summary>
        public static void SetWindowIcon(Microsoft.UI.Xaml.Window window)
        {
            try
            {
                string iconPath = GetIconPath();
                if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                {
                    window.AppWindow.SetIcon(iconPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to set window icon: {ex.Message}");
            }
        }
    }
}

