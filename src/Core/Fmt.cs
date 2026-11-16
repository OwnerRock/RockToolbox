using System;

namespace RockToolbox.Native
{
    internal static class Fmt
    {
        public static string Bytes(double b)
        {
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            while (b >= 1024 && i < u.Length - 1) { b /= 1024; i++; }
            return (i == 0 ? b.ToString("0") : b.ToString("0.#")) + " " + u[i];
        }
    }
}
