using System;

namespace RDownloader.Native
{
    // Task Scheduler through its COM API (Schedule.Service) - replaces Get/Enable/Disable/Unregister-ScheduledTask.
    internal static class TaskSched
    {
        private static dynamic Connect()
        {
            dynamic svc = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
            svc.Connect();
            return svc;
        }

        public static bool Exists(string name)
        {
            try { Connect().GetFolder("\\").GetTask(name); return true; } catch { return false; }
        }

        public static void Delete(string name)
        {
            try { Connect().GetFolder("\\").DeleteTask(name, 0); } catch { }
        }

        // a task that starts the program at logon (normal user rights, no UAC prompt), also on battery, no time limit
        public static void CreateLogon(string name, string exe, string args, string user)
        {
            dynamic svc = Connect();
            dynamic root = svc.GetFolder("\\");
            dynamic def = svc.NewTask(0);
            def.RegistrationInfo.Description = "Starts R-Downloader in the background when you sign in";
            def.Settings.Enabled = true;
            def.Settings.StartWhenAvailable = true;
            def.Settings.DisallowStartIfOnBatteries = false;
            def.Settings.StopIfGoingOnBatteries = false;
            def.Settings.ExecutionTimeLimit = "PT0S";
            def.Settings.MultipleInstances = 2;
            dynamic tr = def.Triggers.Create(9);
            tr.UserId = user;
            tr.Delay = "PT15S";
            dynamic act = def.Actions.Create(0);
            act.Path = exe;
            act.Arguments = args;
            act.WorkingDirectory = System.IO.Path.GetDirectoryName(exe);
            def.Principal.UserId = user;
            def.Principal.LogonType = 3;
            def.Principal.RunLevel = 0;
            root.RegisterTaskDefinition(name, def, 6, null, null, 3);
        }

        // every task directly inside a folder such as \Microsoft\Windows\UpdateOrchestrator
        public static void SetFolderEnabled(string folder, bool enable)
        {
            try
            {
                dynamic f = Connect().GetFolder(folder);
                foreach (dynamic t in f.GetTasks(1))
                    try { t.Enabled = enable; } catch { }
            }
            catch { }
        }

        public static void SetTaskEnabled(string fullPath, bool enable)
        {
            try { Connect().GetFolder("\\").GetTask(fullPath).Enabled = enable; } catch { }
        }

        // delete every task (any folder) whose name starts with the prefix
        public static void DeleteByPrefix(string prefix)
        {
            try { Walk(Connect().GetFolder("\\"), prefix); } catch { }
        }

        private static void Walk(dynamic folder, string prefix)
        {
            try
            {
                foreach (dynamic t in folder.GetTasks(1))
                {
                    string n = Convert.ToString(t.Name);
                    if (n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        try { folder.DeleteTask(n, 0); } catch { }
                }
                foreach (dynamic sub in folder.GetFolders(0)) Walk(sub, prefix);
            }
            catch { }
        }
    }
}
