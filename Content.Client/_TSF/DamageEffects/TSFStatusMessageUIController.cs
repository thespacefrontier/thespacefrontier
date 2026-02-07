using System.Numerics;
using Content.Client.UserInterface.Systems.Gameplay;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._TSF.DamageEffects;

public sealed class TSFStatusMessageUIController : UIController
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IResourceCache _res = default!;

    private LayoutContainer? _root;
    private PanelContainer? _panel;
    private LayoutContainer? _labelContainer;
    private Label? _label;
    private const float ShakeAmount = 3f;
    private const int FontSize = 22;
    /// <summary>Margin from bottom (same style as Hotbar/Inventory use in DefaultGameScreen).</summary>
    private const int BottomMargin = 200;

    public override void Initialize()
    {
        base.Initialize();
        var gameplayLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayLoad.OnScreenLoad += OnScreenLoad;
        gameplayLoad.OnScreenUnload += OnScreenUnload;
    }

    private void OnScreenLoad()
    {
        if (_panel != null)
            return;
        var screen = UIManager.ActiveScreen;
        if (screen == null)
            return;
        var font = LoadStatusMessageFont();
        // Same pattern as Hotbar/Inventory: root positioned with LayoutContainer.BottomWide, content centered via BoxContainer + HorizontalAlignment.Center
        _root = new LayoutContainer { MouseFilter = Control.MouseFilterMode.Ignore };
        LayoutContainer.SetAnchorAndMarginPreset(_root, LayoutContainer.LayoutPreset.BottomWide, margin: BottomMargin);

        var bottomBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            MouseFilter = Control.MouseFilterMode.Ignore,
            Align = BoxContainer.AlignMode.End
        };

        _panel = new PanelContainer
        {
            Visible = false,
            MouseFilter = Control.MouseFilterMode.Ignore,
            HorizontalAlignment = Control.HAlignment.Center,
            MinWidth = 280,
            MaxWidth = 650
        };
        _labelContainer = new LayoutContainer { MouseFilter = Control.MouseFilterMode.Ignore };
        LayoutContainer.SetAnchorPreset(_labelContainer, LayoutContainer.LayoutPreset.Wide);
        _label = new Label
        {
            Text = "",
            Align = Label.AlignMode.Center,
            VerticalAlignment = Control.VAlignment.Center,
            Modulate = new Color(255, 45, 45, 255),
            MinWidth = 280,
            MaxWidth = 600,
            FontOverride = font
        };
        LayoutContainer.SetAnchorAndMarginPreset(_label, LayoutContainer.LayoutPreset.Wide);
        _labelContainer.AddChild(_label);
        _panel.AddChild(_labelContainer);

        bottomBox.AddChild(_panel);
        LayoutContainer.SetAnchorAndMarginPreset(bottomBox, LayoutContainer.LayoutPreset.Wide, margin: 0);
        _root.AddChild(bottomBox);
        screen.AddChild(_root);
    }

    private Font? LoadStatusMessageFont()
    {
        if (_res.TryGetResource<FontResource>("/Fonts/Bahnschrift.ttf", out var fontRes))
            return new VectorFont(fontRes, FontSize);
        if (_res.TryGetResource<FontResource>("/EngineFonts/NotoSans/NotoSans-Regular.ttf", out var fallback))
            return new VectorFont(fallback, FontSize);
        return null;
    }

    private void OnScreenUnload()
    {
        if (_root != null && _root.Parent != null)
        {
            _root.Parent.RemoveChild(_root);
            _root = null;
            _panel = null;
            _labelContainer = null;
            _label = null;
        }
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_label == null || _panel == null || _labelContainer == null)
            return;
        var now = _timing.RealTime.TotalSeconds;
        if (TSFStatusMessageState.Message != null)
        {
            _label.Text = Loc.GetString(TSFStatusMessageState.Message);
            _panel.Visible = true;
            var shakeX = (MathF.Sin((float)(now * 95)) + MathF.Sin((float)(now * 160) * 0.7f)) * ShakeAmount;
            var shakeY = (MathF.Sin((float)(now * 110) + 1.3f) + MathF.Sin((float)(now * 180) * 0.6f)) * ShakeAmount;
            LayoutContainer.SetPosition(_label, new Vector2(shakeX, shakeY));
        }
        else
        {
            _panel.Visible = false;
            LayoutContainer.SetPosition(_label, Vector2.Zero);
        }
    }
}
