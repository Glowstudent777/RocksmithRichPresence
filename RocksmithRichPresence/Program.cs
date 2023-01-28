using DiscordRPC;
using DiscordRPC.Logging;
using RockSnifferLib.Cache;
using RockSnifferLib.Sniffing;
using RockSnifferLib.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using RockSnifferLib.RSHelpers.NoteData;
using RockSnifferLib.RSHelpers;
using System.Linq;

namespace RocksmithRichPresence
{
    public class Program
    {
        private DiscordRpcClient? _discord;
        private RSMemoryReadout readout;
        private SnifferState state = SnifferState.NONE;
        private SongDetails songdetails;

        private readonly Dictionary<string, string> gcadeGames = new Dictionary<string, string>()
        {
            ["GC_WhaleRider"] = "Gone Wailin'!",
            ["GC_StringSkipSaloon"] = "String Skip Saloon",
            ["GC_DucksPlus"] = "Ducks ReDux",
            ["GC_NinjaSlides"] = "Ninja Slide N",
            ["GC_ScaleWarriorsMenu"] = "Scale Warriors",
            ["GC_RailShooterMenu"] = "Return to Chastle Chordead",
            ["GC_TrackAndField"] = "Hurtlin' Hurdles",
            ["GC_TempleOfBends"] = "Temple of Bends",
            ["GC_ScaleRacer"] = "Scale Racer",
            ["GC_StarChords"] = "Star Chords",
            ["GC_HarmonicHeist"] = "Harmonic Heist"
        };

        private DateTime appStartTime;

        static void Main(string[] args)
        {
            var p = new Program();
            Console.Title = "Rocksmith RPC";
            

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

            //_discord = new DiscordRpcClient("1063627185568628776")
            _discord = new DiscordRpcClient("1063627185568628776")
            {
                Logger = new ConsoleLogger { Level = LogLevel.Warning }
            };

            _discord.OnReady += (sender, e) =>
            {
                Logger.Log("[RPC] Received Ready from user {0}", e.User.Username);
                UpdatePresence();
            };

            _discord.Initialize();
            appStartTime = DateTime.UtcNow;

            //Init RockSniffer
            var cache = new MemoryCache();
            var sniffer = new Sniffer(rsProcess, cache);

            //Listen to events
            sniffer.OnStateChanged += Sniffer_OnStateChanged;
            sniffer.OnMemoryReadout += Sniffer_OnMemoryReadout;
            sniffer.OnSongChanged += Sniffer_OnSongChanged;


            while (true)
            {
                if (rsProcess == null || rsProcess.HasExited)
                {
                    break;
                }
            }

            _discord.Dispose();
            _discord = null;


            sniffer.Stop();

            rsProcess?.Dispose();
            rsProcess = null;

            Console.WriteLine("Rocksmith has disappeared");
        }

            private void UpdatePresence()
            {
                //Construct rich presence
                var rp = new RichPresence();
                rp.Assets = new Assets();
                rp.Assets.LargeImageKey = "rocksmith";
                rp.Assets.LargeImageText = "Rocksmith 2014 Remastered";

                //If we have a valid song and are playing a song
                if ((songdetails != null && readout != null) && (state == SnifferState.SONG_STARTING || state == SnifferState.SONG_PLAYING || state == SnifferState.SONG_ENDING))
                {
                    //Get the arrangement based on the arrangement id
                    var arrangement = songdetails.arrangements.FirstOrDefault(x => x.arrangementID == readout.arrangementID);

                    //Add song name
                    rp.Details = $"Playing {songdetails.songName}";

                    //Add artist name
                    rp.State = $"by {songdetails.artistName}";

                    //Set song timer
                    rp.Timestamps = new Timestamps(DateTime.UtcNow, DateTime.UtcNow.AddSeconds(songdetails.songLength - readout.songTimer));

                    //Calculate accuracy
                    float accuracy = readout.noteData.Accuracy;

                    string accuracyText = FormattableString.Invariant($"{accuracy:F2}%");

                    //Set accuracy as text for arrangement icon
                    rp.Assets.SmallImageText = accuracyText;

                    if (readout.mode == RSMode.SCOREATTACK)
                    {
                        var sand = (ScoreAttackNoteData)readout.noteData;

                        rp.Assets.SmallImageText = $"{FormattableString.Invariant($"{sand.CurrentScore:n0}")} x{sand.CurrentMultiplier} | {rp.Assets.SmallImageText}";

                        if (sand.FailedPhrases > 0)
                        {
                            rp.Assets.SmallImageText = $"{new string('X', sand.FailedPhrases)} | {rp.Assets.SmallImageText}";
                        }
                    }

                    //When we got the arrangement
                    if (arrangement != null)
                    {
                        //Set arrangement icon
                        rp.Assets.SmallImageKey = arrangement.type.ToLower();

                        //Try to get section
                        var section = arrangement.sections.LastOrDefault(x => x.startTime < readout.songTimer);

                        //If we got a section
                        if (section != null)
                        {
                            //Add section to small image text
                            rp.Assets.SmallImageText = $"{section.name} | {rp.Assets.SmallImageText}";
                        }
                    }
                }
                else
                {
                    rp.Details = "Browsing Menus";

                    if (readout != null)
                    {
                        string gameStage = readout.gameStage.ToLowerInvariant().Trim();

                        string state = "";

                        if (gameStage.StartsWith("main"))
                        {
                            state = "Main Menu";
                        }
                        else if (gameStage.StartsWith("las"))
                        {
                            state = "Learn A Song";
                        }
                        else if (gameStage.StartsWith("sm"))
                        {
                            state = "Session Mode";
                        }
                        else if (gameStage.StartsWith("nsp"))
                        {
                            state = "Nonstop Play";
                        }
                        else if (gameStage.StartsWith("sa"))
                        {
                            state = "Score Attack";
                        }
                        else if (gameStage.StartsWith("guitarcade") || gameStage.StartsWith("gc_games"))
                        {
                            state = "Guitarcade";
                        }
                        else if (gameStage.StartsWith("gcade"))
                        {
                            rp.Details = "Playing Guitarcade";
                            state = "";

                            if (gcadeGames.ContainsKey(readout.songID))
                            {
                                state = gcadeGames[readout.songID];
                            }
                        }
                        else if (gameStage.StartsWith("ge_"))
                        {
                            state = "Lessons";
                        }
                        else if (gameStage.StartsWith("mp_"))
                        {
                            state = "Multiplayer";
                        }
                        else if (gameStage.StartsWith("shop"))
                        {
                            state = "Shop";
                        }


                        rp.State = state;
                    }

                    rp.Timestamps = new Timestamps(appStartTime);
                }
                _discord.SetPresence(rp);
            }

        private void Sniffer_OnStateChanged(object sender, RockSnifferLib.Events.OnStateChangedArgs e)
        {
            state = e.newState;
            UpdatePresence();
        }

        private void Sniffer_OnSongChanged(object sender, RockSnifferLib.Events.OnSongChangedArgs e)
        {
            songdetails = e.songDetails;
            UpdatePresence();
        }

        private void Sniffer_OnMemoryReadout(object sender, RockSnifferLib.Events.OnMemoryReadoutArgs e)
        {
            readout = e.memoryReadout;
            UpdatePresence();
        }
    }
}
