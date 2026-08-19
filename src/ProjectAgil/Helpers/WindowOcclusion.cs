using System.Runtime.InteropServices;

namespace ProjectAgil.Helpers;

internal static class WindowOcclusion
{
    private const int RootAncestor = 2;
    private const int Columns = 19;
    private const int Rows = 13;

    public static double VisibleFraction(nint window)
    {
        if (window == 0 || !GetWindowRect(window, out var bounds))
        {
            return 1d;
        }

        var width = bounds.Right - bounds.Left;
        var height = bounds.Bottom - bounds.Top;

        if (width <= 0 || height <= 0)
        {
            return 0d;
        }

        _ = GetWindowThreadProcessId(window, out var owner);

        var visible = 0;

        for (var row = 0; row < Rows; row++)
        {
            var y = bounds.Top + (int)((row + 0.5d) * height / Rows);

            for (var column = 0; column < Columns; column++)
            {
                var x = bounds.Left + (int)((column + 0.5d) * width / Columns);
                var hit = WindowFromPoint(new NativePoint { X = x, Y = y });

                if (hit == 0)
                {
                    continue;
                }

                var root = GetAncestor(hit, RootAncestor);

                _ = GetWindowThreadProcessId(root == 0 ? hit : root, out var covering);

                if (covering == owner)
                {
                    visible++;
                }
            }
        }

        return (double)visible / (Rows * Columns);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRect bounds);

    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(NativePoint point);

    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint window, int flags);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}
