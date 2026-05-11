using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;

namespace DriveCrawl_Win32_App.Services
{
    public static class ShellThumbnailService
    {
        private const int ThumbnailWidth = 160;
        private const int ThumbnailHeight = 108;

        [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage([In] SIZE size, [In] SIIGBF flags, [Out] out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int cx, cy; }

        [Flags]
        private enum SIIGBF
        {
            ResizeToFit = 0,
            BiggerSizeOk = 1,
            MemoryOnly = 4,
            IconOnly = 8,
            ThumbnailOnly = 16
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            string pszPath, IntPtr pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private static readonly Guid IID_IShellItem = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");

        public static BitmapSource? GetThumbnail(string path)
        {
            try
            {
                var iid = IID_IShellItem;
                SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out object shellItem);
                var factory = (IShellItemImageFactory)shellItem;
                factory.GetImage(new SIZE { cx = ThumbnailWidth, cy = ThumbnailHeight }, SIIGBF.ResizeToFit, out IntPtr hBitmap);

                if (hBitmap == IntPtr.Zero)
                    return null;

                try
                {
                    var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    src.Freeze();
                    return src;
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
