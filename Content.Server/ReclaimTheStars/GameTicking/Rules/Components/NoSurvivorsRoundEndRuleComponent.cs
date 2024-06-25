using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.ReclaimTheStars.GameTicking.Rules;

[RegisterComponent, Access(typeof(NoSurvivorsRoundEndRuleSystem))]
public sealed partial class NoSurvivorsRoundEndRuleComponent : Component
{
    [DataField]
    public List<EntProtoId> PlayerFactions = ["NanoTrasen"];
}
