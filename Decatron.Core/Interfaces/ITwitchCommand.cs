using TwitchLib.Client;
using TwitchLib.Client.Events;

public interface ITwitchCommand
{
    string CommandName { get; }
    void Execute(OnMessageReceivedArgs e, TwitchClient client);
}
