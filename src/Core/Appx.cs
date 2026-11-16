<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace RockToolbox.Native
{
    internal sealed class AppxInfo
    {
        public string Name, FullName, Version, Publisher;
        public bool IsFramework, NonRemovable, IsSystem;
    }

    // Store (Appx / MSIX) packages through the Windows Runtime PackageManager - no PowerShell cmdlets involved.
    // The WinRT types are bound at run time (late binding), so the program still starts if they are unavailable.
    internal static class Appx
    {
        private static Type PmType()
        {
            Type t = Type.GetType("Windows.Management.Deployment.PackageManager, Windows, ContentType=WindowsRuntime");
            if (t == null) throw new InvalidOperationException("The Windows Store package manager is not available on this PC.");
            return t;
        }

        public static List<AppxInfo> List()
        {
            List<AppxInfo> res = new List<AppxInfo>();
            dynamic pm = Activator.CreateInstance(PmType());
            foreach (dynamic pkg in pm.FindPackages())
            {
                try
                {
                    AppxInfo a = new AppxInfo();
                    dynamic id = pkg.Id;
                    a.Name = Convert.ToString(id.Name);
                    a.FullName = Convert.ToString(id.FullName);
                    a.Publisher = Convert.ToString(id.Publisher);
                    try { dynamic v = id.Version; a.Version = v.Major + "." + v.Minor + "." + v.Build + "." + v.Revision; } catch { a.Version = ""; }
                    try { a.IsFramework = (bool)pkg.IsFramework; } catch { }
                    try { a.NonRemovable = false; } catch { }
                    try { a.IsSystem = Convert.ToInt32(pkg.SignatureKind) == 4; } catch { }
                    Match m = Regex.Match(a.Publisher ?? "", "CN=([^,]+)");
                    if (m.Success) a.Publisher = m.Groups[1].Value;
                    res.Add(a);
                }
                catch { }
            }
            return res;
        }

        // Removes a package for all users when Windows allows it, otherwise for the current user.
        public static void Remove(string fullName)
        {
            Type pt = PmType();
            dynamic pm = Activator.CreateInstance(pt);
            Exception first = null;
            try
            {
                Type ot = Type.GetType("Windows.Management.Deployment.RemovalOptions, Windows, ContentType=WindowsRuntime");
                object opt = Enum.ToObject(ot, 524288);   // RemoveForAllUsers
                WaitDeployment((object)pm.RemovePackageAsync(fullName, opt));
                return;
            }
            catch (Exception ex) { first = ex; }
            try { WaitDeployment((object)pm.RemovePackageAsync(fullName)); }
            catch (Exception ex2) { throw new InvalidOperationException(ex2.Message + (first != null ? " (" + first.Message + ")" : "")); }
        }

        // Add-AppxPackage -RegisterByFamilyName
        public static void RegisterByFamily(string family)
        {
            dynamic pm = Activator.CreateInstance(PmType());
            Type dt = Type.GetType("Windows.Management.Deployment.DeploymentOptions, Windows, ContentType=WindowsRuntime");
            object opt = Enum.ToObject(dt, 0);
            WaitDeployment((object)pm.RegisterPackageByFamilyNameAsync(family, null, opt, null, null));
        }

        // Waits for any Windows Runtime async operation (IAsyncInfo) by polling its Status - no compile-time WinRT reference needed.
        internal static void Wait(object op)
        {
            Type ti = Type.GetType("Windows.Foundation.IAsyncInfo, Windows, ContentType=WindowsRuntime");
            if (ti == null) throw new InvalidOperationException("The Windows Runtime is not available on this PC.");
            System.Reflection.PropertyInfo pStatus = ti.GetProperty("Status"), pError = ti.GetProperty("ErrorCode");
            int guard = 0;
            while (Convert.ToInt32(pStatus.GetValue(op, null)) == 0)
            {
                Thread.Sleep(100);
                if (++guard > 12000) throw new TimeoutException("The Windows Store operation did not finish in time.");
            }
            int st = Convert.ToInt32(pStatus.GetValue(op, null));
            if (st == 2) throw new OperationCanceledException();
            if (st == 3)
            {
                Exception ex = pError.GetValue(op, null) as Exception;
                throw new InvalidOperationException(ex != null ? ex.Message : "The Windows Store operation failed.");
            }
        }

        // a package deployment: after it completes, also read DeploymentResult.ErrorText
        private static void WaitDeployment(object op)
        {
            Wait(op);
            try
            {
                Type gen = Type.GetType("Windows.Foundation.IAsyncOperationWithProgress`2, Windows, ContentType=WindowsRuntime");
                Type tr = Type.GetType("Windows.Management.Deployment.DeploymentResult, Windows, ContentType=WindowsRuntime");
                Type tp = Type.GetType("Windows.Management.Deployment.DeploymentProgress, Windows, ContentType=WindowsRuntime");
                if (gen == null || tr == null || tp == null) return;
                object res = gen.MakeGenericType(tr, tp).GetMethod("GetResults").Invoke(op, null);
                dynamic d = res;
                string err = Convert.ToString(d.ErrorText);
                if (!string.IsNullOrEmpty(err)) throw new InvalidOperationException(err);
            }
            catch (InvalidOperationException) { throw; }
            catch { }
        }
    }
}
=======
﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace RockToolbox.Native
{
    internal sealed class AppxInfo
    {
        public string Name, FullName, Version, Publisher;
        public bool IsFramework, NonRemovable, IsSystem;
    }

    // Store (Appx / MSIX) packages through the Windows Runtime PackageManager - no PowerShell cmdlets involved.
    // The WinRT types are bound at run time (late binding), so the program still starts if they are unavailable.
    internal static class Appx
    {
        private static Type PmType()
        {
            Type t = Type.GetType("Windows.Management.Deployment.PackageManager, Windows, ContentType=WindowsRuntime");
            if (t == null) throw new InvalidOperationException("The Windows Store package manager is not available on this PC.");
            return t;
        }

        public static List<AppxInfo> List()
        {
            List<AppxInfo> res = new List<AppxInfo>();
            dynamic pm = Activator.CreateInstance(PmType());
            foreach (dynamic pkg in pm.FindPackages())
            {
                try
                {
                    AppxInfo a = new AppxInfo();
                    dynamic id = pkg.Id;
                    a.Name = Convert.ToString(id.Name);
                    a.FullName = Convert.ToString(id.FullName);
                    a.Publisher = Convert.ToString(id.Publisher);
                    try { dynamic v = id.Version; a.Version = v.Major + "." + v.Minor + "." + v.Build + "." + v.Revision; } catch { a.Version = ""; }
                    try { a.IsFramework = (bool)pkg.IsFramework; } catch { }
                    try { a.NonRemovable = false; } catch { }
                    try { a.IsSystem = Convert.ToInt32(pkg.SignatureKind) == 4; } catch { }
                    Match m = Regex.Match(a.Publisher ?? "", "CN=([^,]+)");
                    if (m.Success) a.Publisher = m.Groups[1].Value;
                    res.Add(a);
                }
                catch { }
            }
            return res;
        }

        // Removes a package for all users when Windows allows it, otherwise for the current user.
        public static void Remove(string fullName)
        {
            Type pt = PmType();
            dynamic pm = Activator.CreateInstance(pt);
            Exception first = null;
            try
            {
                Type ot = Type.GetType("Windows.Management.Deployment.RemovalOptions, Windows, ContentType=WindowsRuntime");
                object opt = Enum.ToObject(ot, 524288);   // RemoveForAllUsers
                WaitDeployment((object)pm.RemovePackageAsync(fullName, opt));
                return;
            }
            catch (Exception ex) { first = ex; }
            try { WaitDeployment((object)pm.RemovePackageAsync(fullName)); }
            catch (Exception ex2) { throw new InvalidOperationException(ex2.Message + (first != null ? " (" + first.Message + ")" : "")); }
        }

        // Add-AppxPackage -RegisterByFamilyName
        public static void RegisterByFamily(string family)
        {
            dynamic pm = Activator.CreateInstance(PmType());
            Type dt = Type.GetType("Windows.Management.Deployment.DeploymentOptions, Windows, ContentType=WindowsRuntime");
            object opt = Enum.ToObject(dt, 0);
            WaitDeployment((object)pm.RegisterPackageByFamilyNameAsync(family, null, opt, null, null));
        }

        // Waits for any Windows Runtime async operation (IAsyncInfo) by polling its Status - no compile-time WinRT reference needed.
        internal static void Wait(object op)
        {
            Type ti = Type.GetType("Windows.Foundation.IAsyncInfo, Windows, ContentType=WindowsRuntime");
            if (ti == null) throw new InvalidOperationException("The Windows Runtime is not available on this PC.");
            System.Reflection.PropertyInfo pStatus = ti.GetProperty("Status"), pError = ti.GetProperty("ErrorCode");
            int guard = 0;
            while (Convert.ToInt32(pStatus.GetValue(op, null)) == 0)
            {
                Thread.Sleep(100);
                if (++guard > 12000) throw new TimeoutException("The Windows Store operation did not finish in time.");
            }
            int st = Convert.ToInt32(pStatus.GetValue(op, null));
            if (st == 2) throw new OperationCanceledException();
            if (st == 3)
            {
                Exception ex = pError.GetValue(op, null) as Exception;
                throw new InvalidOperationException(ex != null ? ex.Message : "The Windows Store operation failed.");
            }
        }

        // a package deployment: after it completes, also read DeploymentResult.ErrorText
        private static void WaitDeployment(object op)
        {
            Wait(op);
            try
            {
                Type gen = Type.GetType("Windows.Foundation.IAsyncOperationWithProgress`2, Windows, ContentType=WindowsRuntime");
                Type tr = Type.GetType("Windows.Management.Deployment.DeploymentResult, Windows, ContentType=WindowsRuntime");
                Type tp = Type.GetType("Windows.Management.Deployment.DeploymentProgress, Windows, ContentType=WindowsRuntime");
                if (gen == null || tr == null || tp == null) return;
                object res = gen.MakeGenericType(tr, tp).GetMethod("GetResults").Invoke(op, null);
                dynamic d = res;
                string err = Convert.ToString(d.ErrorText);
                if (!string.IsNullOrEmpty(err)) throw new InvalidOperationException(err);
            }
            catch (InvalidOperationException) { throw; }
            catch { }
        }
    }
}
>>>>>>> 2feed756e3ed5d432cbfde84ed40dd41b5f1a37d
