namespace Content.Server.ReclaimTheStars.GameTicking.Rules.Components;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class TwitchIntegrationRuleComponent : Component
{
    public float TimeUntilNextEvent = 0;

    //Everything is configured by cvar instead of component because it makes it easier for an average player to configure
    public float TimeInterval = 60;
    public int EventsQuantity = 3;
    public string ChannelId = "";
}
