using System;
using System.IO;
using System.Media;

namespace RDownloader.Native
{
    // R-Downloader's own sounds (assets\Sound-*.wav, made for this program). Options > Sounds can point any event to another .wav file.
    internal static class RSound
    {
        public static readonly string[][] Events = new string[][]
        {
            new string[] { "Complete",   "Download complete",  "Sound-Complete.wav" },
            new string[] { "Failed",     "Download failed",    "Sound-Failed.wav" },
            new string[] { "QueueStart", "Queue started",      "Sound-QueueStart.wav" },
            new string[] { "QueueEnd",   "Queue finished",     "Sound-QueueEnd.wav" }
        };

        private static SoundPlayer _p;

        public static string DefaultFile(string ev)
        {
            foreach (string[] e in Events) if (e[0] == ev) return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, e[2]);
            return "";
        }

        // the file of an event: the user's own choice, or the built-in sound
        public static string FileOf(string ev)
        {
            string custom = (Ui.Settings.GetString("Snd." + ev) ?? "").Trim();
            if (custom.Length > 0) return custom;
            return DefaultFile(ev);
        }

        public static void Play(string ev) { Play(ev, false); }

        public static void Play(string ev, bool force)
        {
            try
            {
                if (!force && !Ui.Settings.GetBool("SoundsOn", true)) return;
                string f = FileOf(ev);
                if (f.Length == 0 || !File.Exists(f)) return;
                if (_p != null) { try { _p.Stop(); _p.Dispose(); } catch (Exception) { } }
                _p = new SoundPlayer(f);
                _p.Play();
            }
            catch (Exception) { }
        }
    }
}
