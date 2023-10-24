namespace ElevenGPT
{
    class ElevenGPTOptions
    {
        public ulong GuildId { get; set; }

        public string Personality { get; set; }

        public string Voice { get; set; }

        public string ConversationId { get { return GuildId + "|" + Personality; } }

        public string ConversationHeader { get { return $"`{Personality} speaking with the voice of {Voice}:\n`"; } }
    }
}
