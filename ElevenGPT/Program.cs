using System.Diagnostics;
using Discord;
using Discord.Audio;
using Discord.WebSocket;

namespace ElevenGPT
{
    class Program
    {
        private static readonly Dictionary<string, BuildableCommand> slashNameActionMap = new() { };

        private static readonly List<string> personalities = new() { "Personality1" };

        private static readonly List<string> voices = new() { "Voice1" };

        private const GatewayIntents intents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates | GatewayIntents.MessageContent;

        private DiscordSocketClient client = new(new DiscordSocketConfig() { GatewayIntents = intents });

        private string token = string.Empty;

        private string chatGPTToken = string.Empty;

        private string elevenLabsToken = string.Empty;

        private Dictionary<ulong, ElevenGPTOptions> guildOptionsDict = new();

        public static Task Main() => new Program().MainAsync();

        private async Task MainAsync()
        {
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
                Console.Write("Bot token not found, please enter your bot's token: ");
                token = Console.ReadLine() ?? "";
                File.WriteAllText("token", token);
            }

            try
            {
                chatGPTToken = File.ReadAllText("chatgpttoken");
            }
            catch
            {
                Console.Write("OpenAI token not found, please enter your api token: ");
                chatGPTToken = Console.ReadLine() ?? "";
                File.WriteAllText("chatgpttoken", chatGPTToken);
            }

            try
            {
                elevenLabsToken = File.ReadAllText("elevenlabstoken");
            }
            catch
            {
                Console.Write("Eleven Labs token not found, please enter your api token: ");
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
                var channels = guild.TextChannels.Where(channel => channel.Name == "elevengpt-requests");

                if (!channels.Any())
                {
                    Console.WriteLine($"No text channel found for {guild.Name}");
                    continue;
                };

                var channel = channels.First();
                await channel.DeleteMessagesAsync((await channel.GetMessagesAsync().ToListAsync()).First());

                foreach (var commandPair in slashNameActionMap)
                {
                    var slashCommandBuilder = new SlashCommandBuilder();
                    slashCommandBuilder.WithName(commandPair.Key);
                    slashCommandBuilder.WithDescription(commandPair.Value.description);
                    await guild.CreateApplicationCommandAsync(slashCommandBuilder.Build());
                }

                guildOptionsDict.Add(guild.Id, new()
                {
                    Personality = personalities.FirstOrDefault() ?? "",
                    Voice = voices.FirstOrDefault() ?? "",
                });

                var personalityComponentBuilder = new ComponentBuilder()
                    .WithSelectMenu(
                    "Personality",
                    personalities.ConvertAll(
                        personality => new SelectMenuOptionBuilder()
                        .WithValue(personality)
                        .WithLabel(personality)
                        )
                    );
                await channel.SendMessageAsync(text: "Choose a personality:", components: personalityComponentBuilder.Build());

                var voiceComponentBuilder = new ComponentBuilder()
                    .WithSelectMenu(
                    "Voice",
                    voices.ConvertAll(
                        voice => new SelectMenuOptionBuilder()
                        .WithValue(voice)
                        .WithLabel(voice)
                        )
                    );
                await channel.SendMessageAsync(text: "Choose a voice:", components: voiceComponentBuilder.Build());
            }
        }

        private Task SlashCommandExecuted(SocketSlashCommand cmd)
        {
            if (cmd.User is not SocketGuildUser || (cmd.User as SocketGuildUser)!.VoiceChannel == null)
            {
                return Task.CompletedTask;
            }

            var buildableCommand = slashNameActionMap[cmd.CommandName];

            Task.Run(async () =>
            {
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

        private static async Task SpeakAsync(IAudioClient client, string speechFilePath)
        {
            // Create FFmpeg using the previous example
            using (var ffmpeg = CreateStream(speechFilePath))
            using (var output = ffmpeg.StandardOutput.BaseStream)
            using (var discord = client.CreatePCMStream(AudioApplication.Mixed))
            {
                try { await output.CopyToAsync(discord); }
                finally { await discord.FlushAsync(); }
            }
        }

        private static Process CreateStream(string path)
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            })!;
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
