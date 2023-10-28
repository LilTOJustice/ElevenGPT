using System.Diagnostics;
using System.Runtime.ExceptionServices;
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

        private static readonly string basePrompt = "You are to become the character given by the description in the next sentence," +
            " are to respond as that character without ever breaking character, and refrain from multiple paragraph-long responses. ";
        private static Dictionary<string, string> personalities = new() {
            {
                "patrickbateman",
                "Patrick Bateman, the main character of the hit movie, American Psycho."
            },
            {
                "aidencaldwell",
                "Aiden Caldwell, the main character from the video game Dying Light 2."
            },
            {
                "nick",
                "Nick, a recent graduate with a computer science degree, who is incidentally incredibly stupid, but very passionate about music and software engineering."
            },
            {
                "ruvim",
                ""
            },
            {
                "thenarrator",
                "The Narrator from the popular videogame, The Stanley parable."
            },
            {
                "ricksanchez",
                "Rick Sanchez from the show, Rick and Morty."
            }
        };

        private static Dictionary<string, ElevenLabs.Voices.Voice> voices = new();

        private const GatewayIntents intents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates | GatewayIntents.MessageContent | GatewayIntents.GuildMessages;

        private DiscordSocketClient client = new(new DiscordSocketConfig() { GatewayIntents = intents });

        private static ChatGpt? chatGpt = null;

        private static ElevenLabsClient? elevenLabs = null;

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

            chatGpt = new ChatGpt(chatGPTToken);
            elevenLabs = new ElevenLabsClient(elevenLabsToken);

            voices = await GetVoices();

            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            await Task.Delay(-1);
        }

        private async Task<Dictionary<string, ElevenLabs.Voices.Voice>> GetVoices()
        {
            return ((List<ElevenLabs.Voices.Voice>)await elevenLabs!.VoicesEndpoint.GetAllVoicesAsync()).Where(voice => voice.Category == "cloned").ToList().ToDictionary(voice => voice.Name.Replace(" ", string.Empty).ToLower());
        }

        private async Task Ready()
        {
            var voiceCommandBuilder = new SlashCommandBuilder();
            voiceCommandBuilder.WithName("voice").WithDescription("Choose a voice to respond with.");
            foreach (var voice in voices)
            {
                voiceCommandBuilder.AddOption(voice.Key, ApplicationCommandOptionType.SubCommand, "Choose a voice to respond with.");
            }

            var personalityCommandBuilder = new SlashCommandBuilder();
            personalityCommandBuilder.WithName("personality").WithDescription("Choose a voice to respond with.");
            foreach (var personality in personalities)
            {
                personalityCommandBuilder.AddOption(personality.Key, ApplicationCommandOptionType.SubCommand, "Choose a personality to use for the response.");
            }
            personalityCommandBuilder.AddOption("repeat", ApplicationCommandOptionType.SubCommand, "Choose a personality to use for the response.");

            var clearCommandBuilder = new SlashCommandBuilder();
            clearCommandBuilder.WithName("clear-conversation").WithDescription("Clears the conversation with the current personality.");

            var apiCheckCommandBuilder = new SlashCommandBuilder();
            apiCheckCommandBuilder.WithName("api-check").WithDescription("Displays API resource info.");

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

                await guild.CreateApplicationCommandAsync(voiceCommandBuilder.Build());
                await guild.CreateApplicationCommandAsync(personalityCommandBuilder.Build());
                await guild.CreateApplicationCommandAsync(clearCommandBuilder.Build());
                await guild.CreateApplicationCommandAsync(apiCheckCommandBuilder.Build());

                guildOptionsDict.Add(guild.Id, new()
                {
                    GuildId = guild.Id,
                    Personality = "repeat",
                    Voice = voices.FirstOrDefault().Key,
                });

                await channel.SendMessageAsync(guildOptionsDict[guild.Id].ConversationHeader);
            }
        }

        private Task SlashCommandExecuted(SocketSlashCommand cmd)
        {
            if (cmd.User is not SocketGuildUser || (cmd.User as SocketGuildUser)!.VoiceChannel == null)
            {
                return Task.CompletedTask;
            }

            ElevenGPTOptions options = guildOptionsDict[cmd.GuildId ?? 0];

            Task.Run(async () =>
            {
                switch(cmd.CommandName)
                {
                    case "voice":
                        options.Voice = cmd.Data.Options.First().Name;
                        break;
                    case "personality":
                        options.Personality = cmd.Data.Options.First().Name;
                        if (voices.Keys.Contains(options.Personality))
                        {
                            options.Voice = options.Personality;
                        }
                        break;
                    case "clear-conversation":
                        chatGpt!.ResetConversation(options.ConversationId);
                        await chatGpt.Ask(basePrompt + personalities[options.Personality], options.ConversationId);
                        await cmd.RespondAsync($"Cleared conversation with {options.Personality}");
                        return;
                    case "api-check":
                        var subscriptionInfo = await elevenLabs!.UserEndpoint.GetSubscriptionInfoAsync();
                        await cmd.RespondAsync($"ElevenLabs characters used: {subscriptionInfo.CharacterCount}/{subscriptionInfo.CharacterLimit}");
                        return;
                    default:
                        await cmd.RespondAsync("Error, invalid command.");
                        return;
                }

                await cmd.RespondAsync($"I am now speaking as {options.Personality} with the voice of {options.Voice}");
            });

            return Task.CompletedTask;
        }

        private Task MessageReceived(SocketMessage msg)
        {
            if (msg.Author.IsBot || msg.Channel.Name != msgChannelName)
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
                await GPTRespondAsync(msg, options, ((SocketGuildUser)msg.Author).VoiceChannel);
            });

            return Task.CompletedTask;
        }

        private static async Task GPTRespondAsync(SocketMessage msg, ElevenGPTOptions options, SocketVoiceChannel voiceChannel)
        {
            ElevenLabs.Voices.Voice voice = voices[options.Voice];
            string speechPath = "";

            if (options.Personality == "repeat")
            {
                await msg.DeleteAsync();
                await msg.Channel.SendMessageAsync("`Repeating:`\n" + msg.Content);
                speechPath = await elevenLabs!.TextToSpeechEndpoint.TextToSpeechAsync(msg.Content, voice);
            }
            else
            {
                if (!chatGpt!.Conversations.Exists(conversation => conversation.Id == options.ConversationId))
                {
                    await chatGpt.Ask(basePrompt + options.Personality, options.ConversationId);
                }

                string response = await chatGpt.Ask(msg.Content, options.ConversationId);
                await msg.Channel.SendMessageAsync(options.ConversationHeader + response);
                speechPath = await elevenLabs!.TextToSpeechEndpoint.TextToSpeechAsync(response, voice);
            }

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
                Arguments = $"-hide_banner -loglevel quiet -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1 -filter:a \"volume=3.0\"",
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
