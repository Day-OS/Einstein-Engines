using Content.Server.Mind;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server.RoundEnd;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using System.Linq;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Ghost.Roles.Components;
using Content.Server.ReclaimTheStars.GameTicking.Rules.Components;
using Content.Server.StationEvents;
using Content.Server.StationEvents.Components;
using Content.Shared.Chat;
using Content.Shared.Damage;

namespace Content.Server.ReclaimTheStars.GameTicking.Rules;

public class NoSurvivorsRoundEndRuleSystem : GameRuleSystem<NoSurvivorsRoundEndRuleComponent>
{
    [Dependency] private readonly NpcFactionSystem _npcFactionSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly RampingStationEventSchedulerSystem _rampingStationEventSchedulerSystem = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RulePlayerJobsAssignedEvent>(OnPlayerJobAssigned);
        SubscribeLocalEvent<SurvivorComponent, MobStateChangedEvent>(OnSurvivorMobStateChanged);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(HandleLatejoin);
    }

    private void OnSurvivorMobStateChanged(EntityUid _, SurvivorComponent survivorComponent, MobStateChangedEvent args)
    {
        if (args.NewMobState is not MobState.Dead)
            return;

        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out var _, out var comp, out var gameRule))
        {
            if (IsThereAnyValidGhostRolesAvailable(comp))
                return;
            if (!IsCrewDead(comp))
                return;

            _roundEnd.DoRoundEndBehavior(RoundEndBehavior.InstantEnd, TimeSpan.Zero);
            GameTicker.EndGameRule(uid, gameRule);
        }
    }


    private bool IsThereAnyValidGhostRolesAvailable(NoSurvivorsRoundEndRuleComponent comp)
    {
        var query = EntityQueryEnumerator<NpcFactionMemberComponent, GhostTakeoverAvailableComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out _, out _, out _))
        {
            return comp.PlayerFactions.Any(factionName => _npcFactionSystem.ContainsFaction(uid, factionName));
        }
        return false;
    }

    private bool IsCrewDead(NoSurvivorsRoundEndRuleComponent comp)
    {
        var query = EntityQueryEnumerator<MindContainerComponent, MobStateComponent, NpcFactionMemberComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var mindContainerComponent, out var mobStateComponent, out _, out _ ))
        {
            // This eliminates Ghosts
            if (!mindContainerComponent.HasMind)
                continue;

            var containsFaction = comp.PlayerFactions.Any(factionName => _npcFactionSystem.ContainsFaction(uid, factionName));

            if (!containsFaction)
                continue;

            //taking advantage of the fact that we are already using a pretty expensive query to make sure everyone that fits these characteristics gets a survivor badge!
            // Let's say that if this fella right here didn't exist, then there would not be any checks so... if players just decide to play as borgs and they all die, round would run FOREVER until some admin stopped it.
            EnsureComp<SurvivorComponent>(uid);


            if (mobStateComponent.CurrentState != MobState.Dead)
            {
                return false;
            }
        }

        return true;
    }

    protected virtual float Map(float value, float fromLow, float fromHigh, float toLow, float toHigh)
    {
        return (value - fromLow) * (toHigh - toLow) / (fromHigh - fromLow) + toLow;
    }

    private void OnPlayerJobAssigned(RulePlayerJobsAssignedEvent ev)
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out _, out var comp, out _))
        {

            foreach (var player in ev.Players)
            {
                if (player.AttachedEntity == null)
                    return;

                var uid = player.AttachedEntity!.Value;

                SetupSurvivor(uid, comp);
            }

        }
    }



    private void HandleLatejoin(PlayerSpawnCompleteEvent ev)
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out var comp, out _))
        {
            SetupSurvivor(ev.Mob, comp);
        }
    }

    private void SetupSurvivor(EntityUid uid, NoSurvivorsRoundEndRuleComponent comp)
    {
        if (!_mindSystem.TryGetMind(uid, out _, out var mindComponent))
            return;


        if (mindComponent.Session == null)
            return;


        var survivor = EnsureComp<SurvivorComponent>(uid);
        survivor.PlayerFactions = comp.PlayerFactions;


        var briefing = Loc.GetString("coop-greeting");
        ChatManager.ChatMessageToOne(ChatChannel.Server, briefing, briefing, default, false, mindComponent.Session.Channel, Color.Green);
    }


    private void OnRoundEndText(RoundEndTextAppendEvent ev)
    {
        var query = AllEntityQuery<NoSurvivorsRoundEndRuleComponent>();
        var rampingstationquery = EntityQueryEnumerator<RampingStationEventSchedulerComponent>();
        while (query.MoveNext(out _))
        {
            while (rampingstationquery.MoveNext(out var uid, out var rampingStationEventSchedulerComponent))
            {
                var modifier = _rampingStationEventSchedulerSystem.GetChaosModifier(uid, rampingStationEventSchedulerComponent);
                var minChaos = rampingStationEventSchedulerComponent.StartingChaos;
                var maxChaos = rampingStationEventSchedulerComponent.MaxChaos;

                string[] points = ["F-", "F", "F+", "D-", "D", "D+", "C-", "C", "C+", "B-", "B", "B+", "A-", "A", "A+"];

                var pointIndex = (int) Map(modifier, minChaos, maxChaos, 0, points.Length - 1);


                ev.AddLine(Loc.GetString("coop-round-end-text", ("score", points[pointIndex])));

                ev.AddLine(Loc.GetString($"coop-round-score-index-{pointIndex + 1}"));


            }
        }
    }

}
