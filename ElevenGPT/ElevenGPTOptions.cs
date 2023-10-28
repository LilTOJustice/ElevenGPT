namespace ElevenGPT
{
    class ElevenGPTOptions
    {
        public ulong GuildId { get; set; }

        public string Personality { get; set; } = string.Empty;

        public string Voice { get; set; } = string.Empty;

        public string ConversationId { get { return GuildId + "|" + Personality; } }

        public string ConversationHeader
        {
            get
            {
                return Personality == "repeat" ? $"`Repeating with the voice of {Voice}`" : $"`{Personality} speaking with the voice of {Voice}:\n`";
            }
        }
    }
}
