using System.Diagnostics;
using Discord;
using Discord.Audio;
using Discord.WebSocket;

namespace ElevenGPT
{
    class Program
    {
        private static readonly Dictionary<string, BuildableCommand> slashNameActionMap = new () {};

        private static GatewayIntents intents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates | GatewayIntents.MessageContent;

        private DiscordSocketClient client = new(new DiscordSocketConfig() { GatewayIntents = intents });

        private string token = string.Empty;

        private string chatGPTToken = string.Empty;

        private string elevenLabsToken = string.Empty;

        private Dictionary<ulong, ElevenGPTOptions> guildOptionsDict = new();

        public static Task Main() => new Program().MainAsync();

	    private async Task MainAsync() {
            client.Log += Log;
            client.Ready += Ready;
            client.SlashCommandExecuted += SlashCommandExecuted;
            client.MessageReceived += MessageReceived;

            try
            {
                token = File.ReadAllText("token");
            }
            catch
            {
                Console.Write("Token not found, please enter your bot's token: ");
                token = Console.ReadLine() ?? "";
                File.WriteAllText("token", token);
            }

            try
            {
                chatGPTToken = File.ReadAllText("chatgpttoken");
            }
            catch
            {
                Console.Write("Token not found, please enter your bot's token: ");
                chatGPTToken = Console.ReadLine() ?? "";
                File.WriteAllText("chatgpttoken", chatGPTToken);
            }

            try
            {
                elevenLabsToken = File.ReadAllText("elevenlabstoken");
            }
            catch
            {
                Console.Write("Token not found, please enter your bot's token: ");
                elevenLabsToken = Console.ReadLine() ?? "";
                File.WriteAllText("elevenlabstoken", elevenLabsToken);
            }

            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            await Task.Delay(-1);
	    }

        private async Task Ready()
        {
            foreach (var guild in client.Guilds)
            {
                foreach (var commandPair in slashNameActionMap)
                {
                    var guildCommand = new SlashCommandBuilder();
                    guildCommand.WithName(commandPair.Key);
                    guildCommand.WithDescription(commandPair.Value.description);
                    await guild.CreateApplicationCommandAsync(guildCommand.Build());
                }

                guildOptionsDict.Add(guild.Id, new()
                {
                    
                });
            }
        }

        private Task SlashCommandExecuted(SocketSlashCommand cmd)
        {
            if (cmd.User is not SocketGuildUser || (cmd.User as SocketGuildUser)!.VoiceChannel == null) {
                return Task.CompletedTask;
            }

            var buildableCommand = slashNameActionMap[cmd.CommandName];

            Task.Run(async () => {
                    await cmd.RespondAsync(buildableCommand.description);
                    await buildableCommand.callback(cmd);
            });

            return Task.CompletedTask;
        }

        private Task MessageReceived(SocketMessage msg)
        {
            if (msg.Author.IsBot)
            {
                return Task.CompletedTask;
            }

            return Task.CompletedTask;
        }

        private static async Task SpeakAsync(IAudioClient client, string speechFilePath) {
            // Create FFmpeg using the previous example
            using (var ffmpeg = CreateStream(speechFilePath))
            using (var output = ffmpeg.StandardOutput.BaseStream)
            using (var discord = client.CreatePCMStream(AudioApplication.Mixed)) {
                try { await output.CopyToAsync(discord); }
                finally { await discord.FlushAsync(); }
            }
        }

        private static Process CreateStream(string path) {
            return Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            })!;
        }

        private Task Log(LogMessage msg) {
	        Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
