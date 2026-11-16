<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace RockToolbox.Native
{
    // Fast file-system helper (parallel) used by Cleanup: sizes and deletes C:\a\*\b\* style patterns.
    internal static class FastFs
    {
        private static bool IsReparse(FileSystemInfo fi) { return (fi.Attributes & FileAttributes.ReparsePoint) != 0; }

        public static long DirSize(string path)
        {
            long total = 0;
            try
            {
                if (File.Exists(path)) return new FileInfo(path).Length;
                if (!Directory.Exists(path)) return 0;
                Stack<string> st = new Stack<string>();
                st.Push(path);
                while (st.Count > 0)
                {
                    string d = st.Pop();
                    try
                    {
                        DirectoryInfo di = new DirectoryInfo(d);
                        foreach (FileInfo f in di.EnumerateFiles()) { try { total += f.Length; } catch (Exception) { } }
                        foreach (DirectoryInfo sd in di.EnumerateDirectories()) { try { if (!IsReparse(sd)) st.Push(sd.FullName); } catch (Exception) { } }
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception) { }
            return total;
        }

        // expands C:\a\*\b\* style patterns into the real files / folders
        public static List<string> Expand(string pattern)
        {
            List<string> result = new List<string>();
            try
            {
                string[] parts = pattern.Split('\\');
                if (parts.Length < 2) return result;
                List<string> cur = new List<string>();
                cur.Add(parts[0] + "\\");
                for (int i = 1; i < parts.Length; i++)
                {
                    string part = parts[i];
                    if (part.Length == 0) continue;
                    bool last = (i == parts.Length - 1);
                    bool wild = part.IndexOf('*') >= 0 || part.IndexOf('?') >= 0;
                    List<string> next = new List<string>();
                    foreach (string c in cur)
                    {
                        if (!wild)
                        {
                            string p = Path.Combine(c, part);
                            if (last) { if (File.Exists(p) || Directory.Exists(p)) next.Add(p); }
                            else if (Directory.Exists(p)) next.Add(p);
                        }
                        else
                        {
                            try
                            {
                                DirectoryInfo di = new DirectoryInfo(c);
                                if (last) { foreach (FileSystemInfo fi in di.EnumerateFileSystemInfos(part)) next.Add(fi.FullName); }
                                else { foreach (DirectoryInfo sd in di.EnumerateDirectories(part)) next.Add(sd.FullName); }
                            }
                            catch (Exception) { }
                        }
                    }
                    cur = next;
                }
                result = cur;
            }
            catch (Exception) { }
            return result;
        }

        public static long PatternSize(string pattern)
        {
            List<string> items = Expand(pattern);
            long total = 0;
            object lk = new object();
            Parallel.ForEach(items, delegate(string p)
            {
                long sz = DirSize(p);
                lock (lk) { total += sz; }
            });
            return total;
        }

        private static void DeleteTree(DirectoryInfo di)
        {
            try { foreach (FileInfo f in di.EnumerateFiles()) { try { f.Attributes = FileAttributes.Normal; f.Delete(); } catch (Exception) { } } } catch (Exception) { }
            try
            {
                foreach (DirectoryInfo sd in di.EnumerateDirectories())
                {
                    try
                    {
                        if (IsReparse(sd)) { sd.Delete(); }
                        else { DeleteTree(sd); try { sd.Delete(false); } catch (Exception) { } }
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception) { }
        }

        public static void DeleteAny(string p)
        {
            try
            {
                if (File.Exists(p))
                {
                    try { File.SetAttributes(p, FileAttributes.Normal); } catch (Exception) { }
                    File.Delete(p);
                    return;
                }
                if (!Directory.Exists(p)) return;
                try { Directory.Delete(p, true); return; } catch (Exception) { }
                DeleteTree(new DirectoryInfo(p));
                try { Directory.Delete(p, false); } catch (Exception) { }
            }
            catch (Exception) { }
        }

        public static void DeletePattern(string pattern)
        {
            List<string> items = Expand(pattern);
            Parallel.ForEach(items, delegate(string p) { DeleteAny(p); });
        }

        // ---- deleting WITH an honest report: what was removed, what could not be removed and why ----
        public static void DeleteFileTracked(FileInfo f, DelStats st, Action<long> onBytes)
        {
            long len = 0;
            try { len = f.Length; } catch (Exception) { }
            try
            {
                if ((f.Attributes & FileAttributes.ReadOnly) != 0) f.Attributes = FileAttributes.Normal;
                f.Delete();
                System.Threading.Interlocked.Add(ref st.Freed, len);
                System.Threading.Interlocked.Increment(ref st.Deleted);
            }
            catch (UnauthorizedAccessException)
            {
                System.Threading.Interlocked.Add(ref st.Left, len);
                System.Threading.Interlocked.Increment(ref st.Denied);
            }
            catch (IOException ex)
            {
                int hr = 0;
                try { hr = System.Runtime.InteropServices.Marshal.GetHRForException(ex) & 0xFFFF; } catch (Exception) { }
                System.Threading.Interlocked.Add(ref st.Left, len);
                if (hr == 32 || hr == 33) System.Threading.Interlocked.Increment(ref st.InUse);
                else System.Threading.Interlocked.Increment(ref st.Other);
            }
            catch (Exception)
            {
                System.Threading.Interlocked.Add(ref st.Left, len);
                System.Threading.Interlocked.Increment(ref st.Other);
            }
            if (onBytes != null) { try { onBytes(len); } catch (Exception) { } }
        }

        private static void DeleteTreeTracked(DirectoryInfo di, DelStats st, Action<long> onBytes)
        {
            if (Work.CancelRequested) return;
            try { foreach (FileInfo f in di.EnumerateFiles()) { if (Work.CancelRequested) return; DeleteFileTracked(f, st, onBytes); } }
            catch (Exception) { System.Threading.Interlocked.Increment(ref st.Other); }
            try
            {
                foreach (DirectoryInfo sd in di.EnumerateDirectories())
                {
                    if (Work.CancelRequested) return;
                    try
                    {
                        if (IsReparse(sd)) { sd.Delete(); }
                        else { DeleteTreeTracked(sd, st, onBytes); try { sd.Delete(false); } catch (Exception) { } }
                    }
                    catch (Exception) { System.Threading.Interlocked.Increment(ref st.Other); }
                }
            }
            catch (Exception) { System.Threading.Interlocked.Increment(ref st.Other); }
        }

        public static void DeleteRootTracked(string p, DelStats st, Action<long> onBytes)
        {
            try
            {
                if (File.Exists(p)) { DeleteFileTracked(new FileInfo(p), st, onBytes); return; }
                if (!Directory.Exists(p)) return;
                DirectoryInfo di = new DirectoryInfo(p);
                if (IsReparse(di)) { di.Delete(); return; }
                DeleteTreeTracked(di, st, onBytes);
                try { di.Delete(false); } catch (Exception) { }
            }
            catch (Exception) { System.Threading.Interlocked.Increment(ref st.Other); }
        }

        public static void DeletePatternTracked(string pattern, DelStats st, Action<long> onBytes)
        {
            List<string> items = Expand(pattern);
            Parallel.ForEach(items, delegate(string p)
            {
                if (Work.CancelRequested) return;
                DeleteRootTracked(p, st, onBytes);
            });
        }
    }

    // counters filled by the tracked delete methods (fields, so Interlocked can update them from many threads)
    internal sealed class DelStats
    {
        public long Freed;      // bytes really deleted
        public long Left;       // bytes that could not be deleted
        public int Deleted;     // files deleted
        public int InUse;       // files locked by another program
        public int Denied;      // access denied
        public int Other;       // any other failure
        public int Failed { get { return InUse + Denied + Other; } }
    }
}
=======
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace RockToolbox.Native
{
    // Fast file-system helper (parallel) used by Cleanup: sizes and deletes C:\a\*\b\* style patterns.
    internal static class FastFs
    {
        private static bool IsReparse(FileSystemInfo fi) { return (fi.Attributes & FileAttributes.ReparsePoint) != 0; }

        public static long DirSize(string path)
        {
            long total = 0;
            try
            {
                if (File.Exists(path)) return new FileInfo(path).Length;
                if (!Directory.Exists(path)) return 0;
                Stack<string> st = new Stack<string>();
                st.Push(path);
                while (st.Count > 0)
                {
                    string d = st.Pop();
                    try
                    {
                        DirectoryInfo di = new DirectoryInfo(d);
                        foreach (FileInfo f in di.EnumerateFiles()) { try { total += f.Length; } catch (Exception) { } }
                        foreach (DirectoryInfo sd in di.EnumerateDirectories()) { try { if (!IsReparse(sd)) st.Push(sd.FullName); } catch (Exception) { } }
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception) { }
            return total;
        }

        // expands C:\a\*\b\* style patterns into the real files / folders
        public static List<string> Expand(string pattern)
        {
            List<string> result = new List<string>();
            try
            {
                string[] parts = pattern.Split('\\');
                if (parts.Length < 2) return result;
                List<string> cur = new List<string>();
                cur.Add(parts[0] + "\\");
                for (int i = 1; i < parts.Length; i++)
                {
                    string part = parts[i];
                    if (part.Length == 0) continue;
                    bool last = (i == parts.Length - 1);
                    bool wild = part.IndexOf('*') >= 0 || part.IndexOf('?') >= 0;
                    List<string> next = new List<string>();
                    foreach (string c in cur)
                    {
                        if (!wild)
                        {
                            string p = Path.Combine(c, part);
                            if (last) { if (File.Exists(p) || Directory.Exists(p)) next.Add(p); }
                            else if (Directory.Exists(p)) next.Add(p);
                        }
                        else
                        {
                            try
                            {
                                DirectoryInfo di = new DirectoryInfo(c);
                                if (last) { foreach (FileSystemInfo fi in di.EnumerateFileSystemInfos(part)) next.Add(fi.FullName); }
                                else { foreach (DirectoryInfo sd in di.EnumerateDirectories(part)) next.Add(sd.FullName); }
                            }
                            catch (Exception) { }
                        }
                    }
                    cur = next;
                }
                result = cur;
            }
            catch (Exception) { }
            return result;
        }

        public static long PatternSize(string pattern)
        {
            List<string> items = Expand(pattern);
            long total = 0;
            object lk = new object();
            Parallel.ForEach(items, delegate(string p)
            {
                long sz = DirSize(p);
                lock (lk) { total += sz; }
            });
            return total;
        }

        private static void DeleteTree(DirectoryInfo di)
        {
            try { foreach (FileInfo f in di.EnumerateFiles()) { try { f.Attributes = FileAttributes.Normal; f.Delete(); } catch (Exception) { } } } catch (Exception) { }
            try
            {
                foreach (DirectoryInfo sd in di.EnumerateDirectories())
                {
                    try
                    {
                        if (IsReparse(sd)) { sd.Delete(); }
                        else { DeleteTree(sd); try { sd.Delete(false); } catch (Exception) { } }
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception) { }
        }

        public static void DeleteAny(string p)
        {
            try
            {
                if (File.Exists(p))
                {
                    try { File.SetAttributes(p, FileAttributes.Normal); } catch (Exception) { }
                    File.Delete(p);
                    return;
                }
                if (!Directory.Exists(p)) return;
                try { Directory.Delete(p, true); return; } catch (Exception) { }
                DeleteTree(new DirectoryInfo(p));
                try { Directory.Delete(p, false); } catch (Exception) { }
            }
            catch (Exception) { }
        }

        public static void DeletePattern(string pattern)
        {
            List<string> items = Expand(pattern);
            Parallel.ForEach(items, delegate(string p) { DeleteAny(p); });
        }

        // ---- deleting WITH an honest report: what was removed, what could not be removed and why ----
        public static void DeleteFileTracked(FileInfo f, DelStats st, Action<long> onBytes)
        {
            long len = 0;
            try { len = f.Length; } catch (Exception) { }
            try
            {
                if ((f.Attributes & FileAttributes.ReadOnly) != 0) f.Attributes = FileAttributes.Normal;
                f.Delete();
                System.Threading.Interlocked.Add(ref st.Freed, len);
                System.Threading.Interlocked.Increment(ref st.Deleted);
            }
            catch (UnauthorizedAccessException)
            {
                System.Threading.Interlocked.Add(ref st.Left, len);
                System.Threading.Interlocked.Increment(ref st.Denied);
            }
            catch (IOException ex)
            {
                int hr = 0;
                try { hr = System.Runtime.InteropServices.Marshal.GetHRForException(ex) & 0xFFFF; } catch (Exception) { }
                System.Threading.Interlocked.Add(ref st.Left, len);
                if (hr == 32 || hr == 33) System.Threading.Interlocked.Increment(ref st.InUse);
                else System.Threading.Interlocked.Increment(ref st.Other);
            }
            catch (Exception)
            {
                System.Threading.Interlocked.Add(ref st.Left, len);
                System.Threading.Interlocked.Increment(ref st.Other);
            }
            if (onBytes != null) { try { onBytes(len); } catch (Exception) { } }
        }

        private static void DeleteTreeTracked(DirectoryInfo di, DelStats st, Action<long> onBytes)
        {
            if (Work.CancelRequested) return;
            try { foreach (FileInfo f in di.EnumerateFiles()) { if (Work.CancelRequested) return; DeleteFileTracked(f, st, onBytes); } }
            catch (Exception) { System.Threading.Interlocked.Increment(ref st.Other); }
            try
            {
                foreach (DirectoryInfo sd in di.EnumerateDirectories())
                {
                    if (Work.CancelRequested) return;
                    try
                    {
                        if (IsReparse(sd)) { sd.Delete(); }
                        else { DeleteTreeTracked(sd, st, onBytes); try { sd.Delete(false); } catch (Exception) { } }
                    }
                    catch (Exception) { System.Threading.Interlocked.Increment(ref st.Other); }
                }
            }
            catch (Exception) { System.Threading.Interlocked.Increment(ref st.Other); }
        }

        public static void DeleteRootTracked(string p, DelStats st, Action<long> onBytes)
        {
            try
            {
                if (File.Exists(p)) { DeleteFileTracked(new FileInfo(p), st, onBytes); return; }
                if (!Directory.Exists(p)) return;
                DirectoryInfo di = new DirectoryInfo(p);
                if (IsReparse(di)) { di.Delete(); return; }
                DeleteTreeTracked(di, st, onBytes);
                try { di.Delete(false); } catch (Exception) { }
            }
            catch (Exception) { System.Threading.Interlocked.Increment(ref st.Other); }
        }

        public static void DeletePatternTracked(string pattern, DelStats st, Action<long> onBytes)
        {
            List<string> items = Expand(pattern);
            Parallel.ForEach(items, delegate(string p)
            {
                if (Work.CancelRequested) return;
                DeleteRootTracked(p, st, onBytes);
            });
        }
    }

    // counters filled by the tracked delete methods (fields, so Interlocked can update them from many threads)
    internal sealed class DelStats
    {
        public long Freed;      // bytes really deleted
        public long Left;       // bytes that could not be deleted
        public int Deleted;     // files deleted
        public int InUse;       // files locked by another program
        public int Denied;      // access denied
        public int Other;       // any other failure
        public int Failed { get { return InUse + Denied + Other; } }
    }
}
>>>>>>> 2feed756e3ed5d432cbfde84ed40dd41b5f1a37d
