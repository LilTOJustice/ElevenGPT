using System.Diagnostics;
using ChatGPT.Net;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using ElevenLabs;

namespace ElevenGPT
{
    class Program
    {
        private static string msgChannelName = "elevengpt-requests";

        private static readonly Dictionary<string, BuildableCommand> slashNameActionMap = new() { };

        private static readonly string basePrompt = "You are to become the character given by the following description: ";
        private static Dictionary<string, string> personalities = new() {
            {
                "The Narrator",
                "The Narrator from the popular videogame, The Stanley parable."
            }
        };

        private static Dictionary<string, ElevenLabs.Voices.Voice> voices = new();

        private const GatewayIntents intents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates | GatewayIntents.MessageContent | GatewayIntents.GuildMessages;

        private DiscordSocketClient client = new(new DiscordSocketConfig() { GatewayIntents = intents });

        private static string token = string.Empty;

        private static string chatGPTToken = string.Empty;

        private static string elevenLabsToken = string.Empty;

        private Dictionary<ulong, ElevenGPTOptions> guildOptionsDict = new();

        public static Task Main() => new Program().MainAsync();

        private async Task MainAsync()
        {
            client.Log += Log;
            client.Ready += Ready;
            client.SlashCommandExecuted += SlashCommandExecuted;
            client.SelectMenuExecuted += SelectMenuExecuted;
            client.MessageReceived += MessageReceived;

            try
            {
                token = File.ReadAllText("token");
                Console.WriteLine("Got bot token: ", token);
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
                Console.WriteLine("Got ChatGPT token: ", chatGPTToken);
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
                Console.WriteLine("Got ElevenLabs token: ", elevenLabsToken);
            }
            catch
            {
                Console.Write("Eleven Labs token not found, please enter your api token: ");
                elevenLabsToken = Console.ReadLine() ?? "";
                File.WriteAllText("elevenlabstoken", elevenLabsToken);
            }

            voices = await GetVoices();

            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            await Task.Delay(-1);
        }

        private async Task<Dictionary<string, ElevenLabs.Voices.Voice>> GetVoices()
        {
            var api = new ElevenLabsClient(elevenLabsToken);
            return ((List<ElevenLabs.Voices.Voice>)await api.VoicesEndpoint.GetAllVoicesAsync()).Where(voice => voice.Category == "cloned").ToList().ToDictionary(voice => voice.Id);
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
                    PersonalityPrompt = personalities.FirstOrDefault().Value,
                    VoiceId = voices.FirstOrDefault().Key ?? "",
                });

                var personalityComponentBuilder = new ComponentBuilder()
                    .WithSelectMenu(
                    "Personality",
                    personalities.ToList().ConvertAll(
                        personality => new SelectMenuOptionBuilder()
                        .WithValue(personality.Value)
                        .WithLabel(personality.Key)
                        ),
                    personalities.First().Key
                    );
                await channel.SendMessageAsync(text: "Choose a personality:", components: personalityComponentBuilder.Build());

                var voiceComponentBuilder = new ComponentBuilder()
                    .WithSelectMenu(
                    "Voice",
                    voices.ToList().ConvertAll(
                        voice => new SelectMenuOptionBuilder()
                        .WithValue(voice.Value.Id)
                        .WithLabel(voice.Value.Name)
                        ),
                    voices.First().Value.Name
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

        private Task SelectMenuExecuted(SocketMessageComponent socketMessageComponent)
        {
            ElevenGPTOptions guildOptions = guildOptionsDict[socketMessageComponent.GuildId ?? 0];

            Task.Run(async () =>
            {
                if (socketMessageComponent.Data.CustomId == "Personality")
                {
                    guildOptions.PersonalityPrompt = socketMessageComponent.Data.Value;
                }
                else
                {
                    guildOptions.VoiceId = socketMessageComponent.Data.Value;
                }

                await socketMessageComponent.RespondAsync();
            });

            return Task.CompletedTask;
        }

        private Task MessageReceived(SocketMessage msg)
        {
            if (msg.Author.IsBot || msg.Channel.Name != "elevengpt-requests")
            {
                return Task.CompletedTask;
            }

            SocketGuildChannel messageChannel = (SocketGuildChannel)msg.Channel;
            if (messageChannel == null)
            {
                return Task.CompletedTask;
            }

            SocketVoiceChannel voiceChannel = ((SocketGuildUser)msg.Author).VoiceChannel;
            if (voiceChannel == null)
            {
                return Task.CompletedTask;
            }


            ElevenGPTOptions options = guildOptionsDict[messageChannel.Guild.Id];
            Task.Run(async () =>
            {
                await GPTRespondAsync(msg.Content, options, ((SocketGuildUser)msg.Author).VoiceChannel);
            });

            Console.WriteLine($"Recived message for voice: {options.VoiceId} and prompt: {options.PersonalityPrompt}");

            return Task.CompletedTask;
        }

        private static async Task GPTRespondAsync(string text, ElevenGPTOptions options, SocketVoiceChannel voiceChannel)
        {
            ChatGpt chatGpt = new(chatGPTToken);
            ElevenLabsClient elevenLabs = new(elevenLabsToken);
            ElevenLabs.Voices.Voice voice = voices[options.VoiceId];
            string response = await chatGpt.Ask(text);
            string speechPath = await elevenLabs.TextToSpeechEndpoint.TextToSpeechAsync(response, voice);
            IAudioClient audioClient = await voiceChannel.ConnectAsync();
            await SpeakAsync(audioClient, speechPath);
            await voiceChannel.DisconnectAsync();
        }

        private static async Task SpeakAsync(IAudioClient client, string speechFilePath)
        {
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
                Arguments = $"-hide_banner -loglevel quiet -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
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
