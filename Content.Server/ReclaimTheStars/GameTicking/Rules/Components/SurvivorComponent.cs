using Robust.Shared.Prototypes;

namespace Content.Server.ReclaimTheStars.GameTicking.Rules.Components;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class SurvivorComponent : Component
{
    public List<EntProtoId> PlayerFactions = ["NanoTrasen"];
}
