using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using ElevenLabs;
using static System.Net.WebRequestMethods;

namespace ElevenGPT
{
    class Program
    {
        private static readonly Dictionary<string, BuildableCommand> slashNameActionMap = new() { };

        private static List<string> personalities = new() { "test" };

        private static List<ElevenLabs.Voices.Voice> voices = new();

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
                token = System.IO.File.ReadAllText("token");
            }
            catch
            {
                Console.Write("Bot token not found, please enter your bot's token: ");
                token = Console.ReadLine() ?? "";
                System.IO.File.WriteAllText("token", token);
            }

            try
            {
                chatGPTToken = System.IO.File.ReadAllText("chatgpttoken");
            }
            catch
            {
                Console.Write("OpenAI token not found, please enter your api token: ");
                chatGPTToken = Console.ReadLine() ?? "";
                System.IO.File.WriteAllText("chatgpttoken", chatGPTToken);
            }

            try
            {
                elevenLabsToken = System.IO.File.ReadAllText("elevenlabstoken");
            }
            catch
            {
                Console.Write("Eleven Labs token not found, please enter your api token: ");
                elevenLabsToken = Console.ReadLine() ?? "";
                System.IO.File.WriteAllText("elevenlabstoken", elevenLabsToken);
            }

            voices = await GetVoices();
            voices.ForEach(voice => Console.WriteLine(voice.Name));

            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            await Task.Delay(-1);
        }

        private async Task<List<ElevenLabs.Voices.Voice>> GetVoices()
        {
            var api = new ElevenLabsClient(elevenLabsToken);
            return ((List<ElevenLabs.Voices.Voice>)await api.VoicesEndpoint.GetAllVoicesAsync()).Where(voice => voice.Category == "cloned").ToList();
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
                        .WithValue(voice.Id)
                        .WithLabel(voice.Name)
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
