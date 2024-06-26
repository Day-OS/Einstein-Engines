using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.ReclaimTheStars.GameTicking.Rules.Components;
using Content.Server.StationEvents;
using Content.Server.StationEvents.Components;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace Content.Server.ReclaimTheStars.GameTicking.Rules;

public sealed class TwitchIntegrationRuleSystem : GameRuleSystem<TwitchIntegrationRuleComponent>
{
    [Dependency] private readonly EventManagerSystem _event = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;


    private TwitchClient? _client;
    private readonly List<KeyValuePair<EntityPrototype, StationEventComponent>> _currentEvents = [];
    private readonly Dictionary<string, int> _votes = new Dictionary<string, int>();
    private readonly List<KeyValuePair<string, string>> _twitchChatMessages = new List<KeyValuePair<string, string>>();



    protected override void Started(EntityUid uid, TwitchIntegrationRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        var connectionCredentials =
            new ConnectionCredentials(_configurationManager.GetCVar(CCVars.TwitchUsername), _configurationManager.GetCVar(CCVars.TwitchAccessToken));
        var clientOptions = new ClientOptions
        {
            MessagesAllowedInPeriod = 750,
            ThrottlingPeriod = TimeSpan.FromSeconds(30)
        };

        component.TimeInterval = _configurationManager.GetCVar(CCVars.TwitchEventInterval);
        component.ChannelId = _configurationManager.GetCVar(CCVars.TwitchChannel);

        _client = new TwitchClient(new WebSocketClient(clientOptions));
        _client.Initialize(connectionCredentials, component.ChannelId);
        _client.OnConnected += Client_OnConnected!;
        _client.OnMessageReceived += Client_OnMessageReceived!;
        _client.Connect();

        Log.Log(LogLevel.Info, $"SERVER EVENTS ARE: {(_event.EventsEnabled ? "enabled! Have a good game." : "disabled, Twitch Integration WON'T work properly.")}");
    }

    private void Client_OnConnected(object sender, OnConnectedArgs e)
    {
        Log.Log(LogLevel.Info, $"Connected to {e.AutoJoinChannel}");
    }
    private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
    {
        if (!int.TryParse(e.ChatMessage.Message, out var vote))
        {
            _twitchChatMessages.Add(new KeyValuePair<string, string>(e.ChatMessage.Username, e.ChatMessage.Message));
            return;
        }
        if (vote > _currentEvents.Count)
            return;
        _votes[e.ChatMessage.Username] = vote;
        Log.Log(LogLevel.Info, $"User {e.ChatMessage.Username} voted for: {e.ChatMessage.Message}");
    }

    private void PrepareRandomEvents(TwitchIntegrationRuleComponent comp)
    {
        Log.Log(LogLevel.Info, $"Starting events selection.");
        _currentEvents.Clear();
        var availableEvents = _event.AllEvents();
        var eventsList = availableEvents.ToList();
        _random.Shuffle(eventsList);

        for (var i = 0; i < comp.EventsQuantity; i++)
        {
            _currentEvents.Add(eventsList[i]);
        }

        List<string> events = [];

        for (var i = 0; i < _currentEvents.Count; i++)
        {
            events.Add($" {(i+1)} - {_currentEvents[i].Key.ID}");
        }

        var message =
            $"{_currentEvents.Count} Event{(_currentEvents.Count > 1 ? "s" : "")}: {string.Join(" | ", events)}";
        _chatManager.ChatMessageToAll(ChatChannel.Notifications, message, message, EntityUid.Invalid, false, true);

        Log.Log(LogLevel.Info, message);
        _client?.SendMessage(comp.ChannelId, message);
    }

    private void StartMostVotedEvent(TwitchIntegrationRuleComponent comp)
    {
        // <Selected index, Quantity of votes>
        var votesCounter = new Dictionary<int, int>();

        var eventIndex = 0;
        if (_votes.Count > 0)
        {
            foreach (var vote in _votes)
            {
                votesCounter.TryGetValue(vote.Value, out var currentVotes);
                votesCounter[vote.Value] = currentVotes;
            }

            var mostVotedKeyValuePair = votesCounter.MaxBy(kv => kv.Value);
            eventIndex = mostVotedKeyValuePair.Key - 1;
        }

        var mostVoted =  _currentEvents[eventIndex];

        GameTicker.AddGameRule(mostVoted.Key.ID);
        GameTicker.StartGameRule(mostVoted.Key.ID);
        _votes.Clear();
        var message = $"Event {mostVoted.Key.ID} Started!";
        Log.Log(LogLevel.Info, message);
        _client?.SendMessage(comp.ChannelId, message);
    }

    protected override void ActiveTick(EntityUid uid, TwitchIntegrationRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (!GameTicker.IsGameRuleActive(uid, gameRule))
            return;

        //HACKY WAY OF GETTING TWITCH CHAT MESSAGES!!
        if (_twitchChatMessages.Count > 0)
        {
            foreach (var chatMessage in _twitchChatMessages)
            {
                var message = $"TWITCH - {chatMessage.Key}: {chatMessage.Value}";
                _chatManager.ChatMessageToAll(ChatChannel.Notifications, message, message, EntityUid.Invalid, false, true);
            }

            _twitchChatMessages.Clear();
        }


        component.TimeUntilNextEvent -= frameTime;

        if ((component.TimeUntilNextEvent > 0f))
            return;


        //Aka if any voting has been started before
        if (_votes.Count > 0)
        {
            StartMostVotedEvent(component);
        }

        PrepareRandomEvents(component);

        component.TimeUntilNextEvent = component.TimeInterval;


    }
    protected override void Ended(EntityUid uid, TwitchIntegrationRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        Log.Log(LogLevel.Info, "Twitch Integration System shutting down.");

    }

}
