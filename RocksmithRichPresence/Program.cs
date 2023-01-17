using DiscordRPC;
using DiscordRPC.Logging;
using RockSnifferLib.Cache;
using RockSnifferLib.Sniffing;
using System;
using System.Diagnostics;
using System.Threading;

namespace RocksmithRichPresence
{
    public class Program
    {
        private DiscordRpcClient _discord;

        static void Main(string[] args)
        {
            var p = new Program();

            while (true)
            {
                try
                {
                    p.Run();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    throw;
                }
            }
        }

        private void Run()
        {
            Console.WriteLine("Waiting for Rocksmith...");

            Process? rsProcess = null;

            while (true)
            {
                var processes = Process.GetProcessesByName("Rocksmith2014");

                if (processes.Length == 0)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                rsProcess = processes[0];
                if (rsProcess.HasExited || !rsProcess.Responding)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                break;
            }

            Console.WriteLine("Rocksmith 2014 Detected");

            _discord = new DiscordRpcClient("1063627185568628776")
            {
                Logger = new ConsoleLogger { Level = LogLevel.Warning }
            };

            _discord.OnReady += (sender, e) =>
            {
                Console.WriteLine("Received Ready from user {0}", e.User.Username);
            };

            _discord.OnPresenceUpdate += (sender, e) =>
            {
                Console.WriteLine("Received Update! {0}", e.Presence);
            };

            _discord.Initialize();

            _discord.SetPresence(new RichPresence()
            {
                Details = "Rocksmith 2014",
                State = SnifferState.IN_MENUS.ToHuman(),
                Assets = new Assets()
                {
                    LargeImageKey = "logo"
                }
            });

            _discord.UpdateLargeAsset("logo");
            _discord.UpdateState(SnifferState.IN_MENUS.ToHuman());

             //Init RockSniffer
            var cache = new MemoryCache();
            var sniffer = new Sniffer(rsProcess, cache);

            sniffer.OnSongChanged += Sniffer_OnSongChanged;
            sniffer.OnStateChanged += Sniffer_OnStateChanged;

            while (true)
            {
                if (rsProcess == null || rsProcess.HasExited)
                {
                    break;
                }
            }

            _discord.Dispose();
            _discord = null;

            sniffer.OnSongChanged -= Sniffer_OnSongChanged;
            sniffer.OnStateChanged -= Sniffer_OnStateChanged;

            sniffer.Stop();

            rsProcess.Dispose();
            rsProcess = null;

            Console.WriteLine("Rocksmith has disappeared");
        }

        private void Sniffer_OnStateChanged(object sender, RockSnifferLib.Events.OnStateChangedArgs e)
        {
            _discord.UpdateState(e.newState.ToHuman());
        }

        private void Sniffer_OnSongChanged(object sender, RockSnifferLib.Events.OnSongChangedArgs e)
        {
            _discord.UpdateDetails($"{e.songDetails.artistName} - {e.songDetails.songName}");
        }
    }

    static class Extensions
    {
        public static string ToHuman(this SnifferState state)
        {
            switch (state)
            {
                case SnifferState.IN_MENUS: return "Browsing the menus";
                case SnifferState.SONG_ENDING: return "Song ending";
                case SnifferState.SONG_PLAYING: return "Playing a song";
                case SnifferState.SONG_SELECTED: return "Preparing to play a song";
                case SnifferState.SONG_STARTING: return "Song starting";
                default: return "";
            }
        }
    }
}
