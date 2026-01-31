using Content.Shared.Medical;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Client._TSF.Medical;

public sealed class TSFDefibrillatorClientSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly SoundPathSpecifier ResuscitatedSound =
        new("/Audio/_TSF/Defibrillator/resuscitated_heartbeat.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<DefibrillatedTargetSoundEvent>(OnDefibrillatedTargetSound);
    }

    private void OnDefibrillatedTargetSound(DefibrillatedTargetSoundEvent ev)
    {
        _audio.PlayGlobal(ResuscitatedSound, Filter.Local(), false);
    }
}
