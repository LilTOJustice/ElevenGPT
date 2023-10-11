using Discord.WebSocket;

namespace ElevenGPT
{
    struct BuildableCommand {
        public BuildableCommand(string _description, Func<SocketSlashCommand, Task> _callback) {
            description = _description;
            callback = _callback;
        }

        public string description;
        public Func<SocketSlashCommand, Task> callback;
    }
}
