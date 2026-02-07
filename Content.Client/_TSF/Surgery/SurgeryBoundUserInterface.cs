using Content.Shared._TSF.Surgery;
using Robust.Client.UserInterface;

namespace Content.Client._TSF.Surgery;

public sealed class SurgeryBoundUserInterface : BoundUserInterface
{
    private SurgeryWindow? _window;

    public SurgeryBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindowCenteredLeft<SurgeryWindow>();
        _window.OnActionRequested += (part, action) =>
            SendMessage(new SurgeryActionRequestMessage(part, action));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is SurgeryBuiState cast && _window != null)
            _window.Populate(cast);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _window?.Close();
    }
}
