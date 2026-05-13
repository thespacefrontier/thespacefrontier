// Copyright (C) 2026 insvrg3ncy
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.UserInterface.Systems.Gameplay;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Localization;
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
    private const int BottomMargin = 200;
    private static readonly Color StatusTint = new(255, 45, 45, 255);
    private const float HideFadeSeconds = 0.42f;

    private string? _revealLocId;
    private string? _revealFullText;
    private int _revealTotalRunes;
    private int _revealVisibleRunes;
    private float _revealCarrySeconds;

    private float _hideFadeRemaining;
    private string? _hideFadeText;

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
            _revealLocId = null;
            _revealFullText = null;
            _hideFadeRemaining = 0f;
            _hideFadeText = null;
        }
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_label == null || _panel == null || _labelContainer == null)
            return;
        var now = _timing.RealTime.TotalSeconds;
        var msg = TSFStatusMessageState.Message;
        if (msg != null)
        {
            _hideFadeRemaining = 0f;
            _hideFadeText = null;

            if (msg != _revealLocId)
            {
                _revealLocId = msg;
                _revealFullText = Loc.GetString(msg);
                _revealTotalRunes = CountRunes(_revealFullText);
                _revealVisibleRunes = 0;
                _revealCarrySeconds = 0f;
            }

            _revealCarrySeconds += args.DeltaSeconds;
            var step = TSFStatusMessageState.RevealSecondsPerRune;
            while (_revealVisibleRunes < _revealTotalRunes && _revealCarrySeconds >= step)
            {
                _revealCarrySeconds -= step;
                _revealVisibleRunes++;
            }

            _label.Text = TakeFirstRunes(_revealFullText!, _revealVisibleRunes);
            _label.Modulate = StatusTint;
            _panel.Visible = true;
            var shakeX = (MathF.Sin((float)(now * 95)) + MathF.Sin((float)(now * 160) * 0.7f)) * ShakeAmount;
            var shakeY = (MathF.Sin((float)(now * 110) + 1.3f) + MathF.Sin((float)(now * 180) * 0.6f)) * ShakeAmount;
            LayoutContainer.SetPosition(_label, new Vector2(shakeX, shakeY));
        }
        else
        {
            if (_hideFadeRemaining <= 0f)
            {
                if (!string.IsNullOrEmpty(_revealFullText))
                {
                    _hideFadeText = _revealFullText;
                    _hideFadeRemaining = HideFadeSeconds;
                }
                else if (_panel.Visible && !string.IsNullOrEmpty(_label.Text))
                {
                    _hideFadeText = _label.Text;
                    _hideFadeRemaining = HideFadeSeconds;
                }
            }

            _revealLocId = null;
            _revealFullText = null;
            _revealTotalRunes = 0;
            _revealVisibleRunes = 0;
            _revealCarrySeconds = 0f;

            if (_hideFadeRemaining > 0f)
            {
                _hideFadeRemaining -= args.DeltaSeconds;
                var alphaNorm = Math.Clamp(Math.Max(0f, _hideFadeRemaining) / HideFadeSeconds, 0f, 1f);
                _label.Text = _hideFadeText ?? string.Empty;
                _label.Modulate = StatusTint.WithAlpha(alphaNorm);
                _panel.Visible = true;
                var shakeScale = alphaNorm;
                var shakeX = (MathF.Sin((float)(now * 95)) + MathF.Sin((float)(now * 160) * 0.7f)) * ShakeAmount * shakeScale;
                var shakeY = (MathF.Sin((float)(now * 110) + 1.3f) + MathF.Sin((float)(now * 180) * 0.6f)) * ShakeAmount * shakeScale;
                LayoutContainer.SetPosition(_label, new Vector2(shakeX, shakeY));

                if (_hideFadeRemaining <= 0f)
                {
                    _hideFadeText = null;
                    _label.Text = string.Empty;
                    _label.Modulate = StatusTint;
                    _panel.Visible = false;
                    LayoutContainer.SetPosition(_label, Vector2.Zero);
                }
            }
            else
            {
                _hideFadeText = null;
                _label.Text = string.Empty;
                _label.Modulate = StatusTint;
                _panel.Visible = false;
                LayoutContainer.SetPosition(_label, Vector2.Zero);
            }
        }
    }

    private static int CountRunes(string s)
    {
        var n = 0;
        foreach (var _ in s.EnumerateRunes())
            n++;
        return n;
    }

    private static string TakeFirstRunes(string s, int runeCount)
    {
        if (runeCount <= 0)
            return string.Empty;

        var seen = 0;
        var endUtf16 = 0;
        foreach (var r in s.EnumerateRunes())
        {
            if (seen >= runeCount)
                break;
            endUtf16 += r.Utf16SequenceLength;
            seen++;
        }

        return s[..endUtf16];
    }
}
