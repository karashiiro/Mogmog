using Dalamud.Game.Chat;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Grpc.Core;
using Grpc.Net.Client;
using Mogmog.Protos;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static Mogmog.Protos.ChatService;

namespace Mogmog.FFXIV
{
    public class Mogmog : IDalamudPlugin
    {
        public string Name => "Mogmog";

        private const string command1 = "/global";
        private const string command2 = "/gl";

        private const string hostname = "https://localhost:5001"; // Temporary, use Imgui

        private AsyncDuplexStreamingCall<ChatMessage, ChatMessage> chatStream;
        private ChatServiceClient client;
        private DalamudPluginInterface dalamud;
        private GrpcChannel channel;
        private MogmogConfiguration config;

        private Task runningTask;

        private string CharacterSearch { get => $"https://xivapi.com/character/search?name={this.dalamud.ClientState.LocalPlayer.Name}&server={this.dalamud.ClientState.LocalPlayer.HomeWorld.Name}"; }
        private string avatar;

        public void Initialize(DalamudPluginInterface dalamud)
        {
            this.dalamud = dalamud;
            this.config = dalamud.GetPluginConfig() as MogmogConfiguration ?? new MogmogConfiguration();

            dalamud.CommandManager.AddHandler(command1, OnMessageCommandInfo());
            dalamud.CommandManager.AddHandler(command2, OnMessageCommandInfo());

            this.channel = GrpcChannel.ForAddress(hostname);
            this.client = new ChatServiceClient(this.channel);
            this.chatStream = this.client.Chat();

            this.avatar = JObject.Parse((new HttpClient()).GetStringAsync(new Uri(CharacterSearch)).Result)["Results"][0]["Avatar"].ToObject<string>();

            this.runningTask = ChatLoop();
        }

        private async Task ChatLoop()
        {
            while (true)
            {
                if (await this.chatStream.ResponseStream.MoveNext())
                {
                    MessageReceive(this.chatStream.ResponseStream.Current);
                }
            }
        }

        private void MessageSend(string _, string message)
        {
            this.chatStream.RequestStream.WriteAsync(new ChatMessage
            {
                Id = 0,
                Content = message,
                Author = this.dalamud.ClientState.LocalPlayer.Name,
                AuthorId = this.dalamud.ClientState.LocalContentId,
                Avatar = this.avatar,
                World = null,
                WorldId = this.dalamud.ClientState.LocalPlayer.HomeWorld.Id
            });
        }

        private void MessageReceive(ChatMessage message)
        {
            this.dalamud.Framework.Gui.Chat.PrintChat(new XivChatEntry
            {
                Name = message.Author + " (" + message.World + ")", // todo: use CW icon
                MessageBytes = Encoding.UTF8.GetBytes(message.Content),
                Type = XivChatType.Notice,
            });
            this.dalamud.Framework.Gui.Chat.UpdateQueue(this.dalamud.Framework);
        }

        private CommandInfo OnMessageCommandInfo()
        {
            return new CommandInfo(MessageSend)
            {
                HelpMessage = "Sends a message to the Mogmog global chat.",
                ShowInHelp = true,
            };
        }

        public void Dispose()
        {
            this.runningTask.Dispose();

            this.chatStream.Dispose();
            this.channel.Dispose();

            this.dalamud.CommandManager.RemoveHandler(command1);
            this.dalamud.CommandManager.RemoveHandler(command2);

            this.dalamud.SavePluginConfig(this.config);

            this.dalamud.Dispose();
        }
    }
}
