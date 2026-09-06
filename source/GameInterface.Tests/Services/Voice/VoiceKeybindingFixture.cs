using Moq;
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.InputSystem;
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
    private readonly bool previousOnScreenKeyboard;

    public Mock<IInputManager> InputManager { get; } = new();

    public VoiceKeybindingFixture()
    {
        previousInputState = inputFields.Select(field => field.GetValue(null)).ToArray();
        previousModule = GameModule.CurrentModule;
        previousOnScreenKeyboard = Input.IsOnScreenKeyboardActive;
        try
        {
            InputManager.Setup(input => input.GetVirtualKeyCode(It.IsAny<InputKey>())).Returns(0);
            Input.IsOnScreenKeyboardActive = false;
            Input.Initialize(InputManager.Object, null);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        for (int index = 0; index < inputFields.Length; index++)
            inputFields[index].SetValue(null, previousInputState[index]);
        Input.IsOnScreenKeyboardActive = previousOnScreenKeyboard;
        GameModule.CurrentModule = previousModule;
    }
}
