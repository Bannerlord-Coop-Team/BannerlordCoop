using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Localization;
using GameModule = TaleWorlds.MountAndBlade.Module;

namespace GameInterface.Tests.Services.Voice;

public sealed class VoiceKeybindingFixture : IDisposable
{
    // InputSystem is not publicized; preserve every static field changed by Input.Initialize.
    private readonly FieldInfo[] inputFields = new[]
    {
        "_inputManager", "_emptyInputManager", "<InputState>k__BackingField",
        "keyData", "<DebugInput>k__BackingField"
    }.Select(name => typeof(Input).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!).ToArray();
    private readonly object?[] previousInputState;
    private readonly GameModule previousModule;
    private readonly GameTextManager? previousTextManager;
    private readonly bool previousOnScreenKeyboard;

    public Mock<IInputManager> InputManager { get; } = new();

    public VoiceKeybindingFixture()
    {
        previousInputState = inputFields.Select(field => field.GetValue(null)).ToArray();
        previousModule = GameModule.CurrentModule;
        previousTextManager = previousModule?.GlobalTextManager;
        previousOnScreenKeyboard = Input.IsOnScreenKeyboardActive;
        try
        {
            InputManager.Setup(input => input.GetVirtualKeyCode(It.IsAny<InputKey>())).Returns(0);
            Input.IsOnScreenKeyboardActive = false;
            Input.Initialize(InputManager.Object, null);
            if (previousModule != null) previousModule.GlobalTextManager = CreateKeyTexts();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private GameTextManager CreateKeyTexts()
    {
        var texts = new GameTextManager();
        var keyTexts = texts.AddGameText("str_game_key_text");
        foreach (var key in new[] { InputKey.Q, InputKey.F11, InputKey.F12, InputKey.LeftMouseButton })
            keyTexts.AddVariationWithId(key.ToString().ToLowerInvariant(),
                new TextObject("{=!}" + key), new List<GameTextManager.ChoiceTag>());
        return texts;
    }

    public void Dispose()
    {
        for (int index = 0; index < inputFields.Length; index++)
            inputFields[index].SetValue(null, previousInputState[index]);
        Input.IsOnScreenKeyboardActive = previousOnScreenKeyboard;
        if (previousModule != null) previousModule.GlobalTextManager = previousTextManager;
        GameModule.CurrentModule = previousModule;
    }
}
