using System;
using TaleWorlds.Library;

namespace GameInterface.Services.Chat;

/// <summary>One fading feed line in the co-op chat / event log.</summary>
internal sealed class ChatLineVM : ViewModel
{
    private const float VisibilityDuration = 10f;
    private const float FadeOutDuration = 0.5f;

    private readonly bool isPlayerChat;
    private float timeSinceCreation;
    private bool forcedVisible;
    private string text;
    private Color color;
    private float alpha;

    public ChatLineVM(string text, Color color, bool isPlayerChat)
    {
        this.isPlayerChat = isPlayerChat;
        Text = text ?? string.Empty;
        Color = color;
        Alpha = 1f;
    }

    public bool IsPlayerChat => isPlayerChat;

    [DataSourceProperty]
    public string Text
    {
        get => text;
        private set
        {
            if (text == value) return;
            text = value;
            OnPropertyChanged(nameof(Text));
        }
    }

    [DataSourceProperty]
    public Color Color
    {
        get => color;
        private set
        {
            if (color == value) return;
            color = value;
            OnPropertyChanged(nameof(Color));
        }
    }

    [DataSourceProperty]
    public float Alpha
    {
        get => alpha;
        private set
        {
            if (Math.Abs(alpha - value) < 0.0001f) return;
            alpha = value;
            OnPropertyChanged(nameof(Alpha));
        }
    }

    public void HandleFading(float dt)
    {
        timeSinceCreation += dt;
        RefreshAlpha();
    }

    public void ToggleForceVisible(bool visible)
    {
        forcedVisible = visible;
        RefreshAlpha();
    }

    internal static float ComputeAlpha(float timeSinceCreation, bool forcedVisible)
    {
        if (forcedVisible) return 1f;
        if (timeSinceCreation >= VisibilityDuration)
            return MBMath.ClampFloat(1f - (timeSinceCreation - VisibilityDuration) / FadeOutDuration, 0f, 1f);
        return 1f;
    }

    private void RefreshAlpha()
    {
        Alpha = ComputeAlpha(timeSinceCreation, forcedVisible);
    }
}