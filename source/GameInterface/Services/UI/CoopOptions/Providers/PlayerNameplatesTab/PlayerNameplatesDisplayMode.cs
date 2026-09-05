using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameInterface.Services.UI.CoopOptions.Providers.PlayerNameplatesTab;

/// <summary>How a client chooses to display player nameplates in missions.</summary>
public enum PlayerNameplatesDisplayMode
{
    Always = 0,
    HoldIndicators = 1,
    Never = 2,
}

/// <summary>Reads the legacy bool (<c>true</c>/<c>false</c>) as Always/Never, plus enum strings.</summary>
public sealed class PlayerNameplatesDisplayModeConverter : JsonConverter<PlayerNameplatesDisplayMode>
{
    public override PlayerNameplatesDisplayMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True:
                return PlayerNameplatesDisplayMode.Always;
            case JsonTokenType.False:
                return PlayerNameplatesDisplayMode.Never;
            case JsonTokenType.Number:
                if (reader.TryGetInt32(out int numeric)
                    && Enum.IsDefined(typeof(PlayerNameplatesDisplayMode), numeric))
                    return (PlayerNameplatesDisplayMode)numeric;
                break;
            case JsonTokenType.String:
                string text = reader.GetString();
                if (bool.TryParse(text, out bool flag))
                    return flag ? PlayerNameplatesDisplayMode.Always : PlayerNameplatesDisplayMode.Never;
                if (Enum.TryParse<PlayerNameplatesDisplayMode>(text, true, out var mode))
                    return mode;
                break;
        }

        throw new JsonException($"Unable to convert token {reader.TokenType} to {nameof(PlayerNameplatesDisplayMode)}.");
    }

    public override void Write(Utf8JsonWriter writer, PlayerNameplatesDisplayMode value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
