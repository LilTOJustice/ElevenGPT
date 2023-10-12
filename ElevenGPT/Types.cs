namespace ElevenGPT
{
    public record class VoicesResponse
    {
        public record class Voices
        {
            public string? voice_id = null;
            public string? name = null;
            public string? category = null;
        }

        public Voices[]? voices = null;
    }
}
