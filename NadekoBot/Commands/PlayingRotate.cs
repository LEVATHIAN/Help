﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using System.Timers;
using NadekoBot.Modules;

namespace NadekoBot.Commands {
    internal class PlayingRotate : IDiscordCommand {
        private static readonly Timer timer = new Timer(12000);

        public static Dictionary<string, Func<string>> PlayingPlaceholders { get; } =
            new Dictionary<string, Func<string>> {
                {"%servers%", () => NadekoBot.Client.Servers.Count().ToString()},
                {"%users%", () => NadekoBot.Client.Servers.SelectMany(s => s.Users).Count().ToString()},
                {"%playing%", () => {
                        var cnt = Music.MusicPlayers.Count(kvp => kvp.Value.CurrentSong != null);
                        if (cnt != 1) return cnt.ToString();
                        try {
                            var mp = Music.MusicPlayers.FirstOrDefault();
                            return mp.Value.CurrentSong.SongInfo.Title;
                        }
                        catch {
                            return "No songs";
                        }
                    }
                },
                {"%queued%", () => Music.MusicPlayers.Sum(kvp => kvp.Value.Playlist.Count).ToString()},
                {"%trivia%", () => Trivia.RunningTrivias.Count.ToString()}
            };

        private readonly object playingPlaceholderLock = new object();

        public PlayingRotate() {
            var i = -1;
            timer.Elapsed += (s, e) => {
                try {
                    i++;
                    var status = "";
                    lock (playingPlaceholderLock) {
                        if (PlayingPlaceholders.Count == 0)
                            return;
                        if (i >= PlayingPlaceholders.Count) {
                            i = -1;
                            return;
                        }
                        status = NadekoBot.Config.RotatingStatuses[i];
                        status = PlayingPlaceholders.Aggregate(status,
                            (current, kvp) => current.Replace(kvp.Key, kvp.Value()));
                    }
                    if (string.IsNullOrWhiteSpace(status))
                        return;
                    Task.Run(() => { NadekoBot.Client.SetGame(status); });
                } catch { }
            };

            timer.Enabled = NadekoBot.Config.IsRotatingStatus;
        }

        public Func<CommandEventArgs, Task> DoFunc() => async e => {
            lock (playingPlaceholderLock) {
                if (timer.Enabled)
                    timer.Stop();
                else
                    timer.Start();
                NadekoBot.Config.IsRotatingStatus = timer.Enabled;
                NadekoBot.SaveConfig();
            }
            await e.Channel.SendMessage($"❗`Rotating playing status has been {(timer.Enabled ? "enabled" : "disabled")}.`");
        };

        public void Init(CommandGroupBuilder cgb) {
            cgb.CreateCommand(".rotateplaying")
                .Alias(".ropl")
                .Description("Toggles rotation of playing status of the dynamic strings you specified earlier.")
                .AddCheck(Classes.Permissions.SimpleCheckers.OwnerOnly())
                .Do(DoFunc());

            cgb.CreateCommand(".addplaying")
                .Alias(".adpl")
                .Description("Adds a specified string to the list of playing strings to rotate. " +
                             "Supported placeholders: " + string.Join(", ", PlayingPlaceholders.Keys))
                .Parameter("text", ParameterType.Unparsed)
                .AddCheck(Classes.Permissions.SimpleCheckers.OwnerOnly())
                .Do(async e => {
                    var arg = e.GetArg("text");
                    if (string.IsNullOrWhiteSpace(arg))
                        return;
                    lock (playingPlaceholderLock) {
                        NadekoBot.Config.RotatingStatuses.Add(arg);
                        NadekoBot.SaveConfig();
                    }
                    await e.Channel.SendMessage("🆗 `Added a new playing string.`");
                });

            cgb.CreateCommand(".listplaying")
                .Alias(".lipl")
                .Description("Lists all playing statuses with their corresponding number.")
                .AddCheck(Classes.Permissions.SimpleCheckers.OwnerOnly())
                .Do(async e => {
                    if (NadekoBot.Config.RotatingStatuses.Count == 0)
                        await e.Channel.SendMessage("`There are no playing strings. " +
                                                    "Add some with .addplaying [text] command.`");
                    var sb = new StringBuilder();
                    for (var i = 0; i < NadekoBot.Config.RotatingStatuses.Count; i++) {
                        sb.AppendLine($"`{i + 1}.` {NadekoBot.Config.RotatingStatuses[i]}");
                    }
                    await e.Channel.SendMessage(sb.ToString());
                });

            cgb.CreateCommand(".removeplaying")
                .Alias(".repl", ".rmpl")
                .Description("Removes a playing string on a given number.")
                .Parameter("number", ParameterType.Required)
                .AddCheck(Classes.Permissions.SimpleCheckers.OwnerOnly())
                .Do(async e => {
                    var arg = e.GetArg("number");
                    int num;
                    string str;
                    lock (playingPlaceholderLock) {
                        if (!int.TryParse(arg.Trim(), out num) || num <= 0 || num > NadekoBot.Config.RotatingStatuses.Count)
                            return;
                        str = NadekoBot.Config.RotatingStatuses[num - 1];
                        NadekoBot.Config.RotatingStatuses.RemoveAt(num - 1);
                        NadekoBot.SaveConfig();
                    }
                    await e.Channel.SendMessage($"🆗 `Removed playing string #{num}`({str})");
                });
        }
    }
}
