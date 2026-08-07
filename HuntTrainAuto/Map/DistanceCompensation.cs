#nullable enable
using System.Numerics;

namespace HuntTrainAuto.Map;

/// <summary>
/// HTA <c>getDistanceCompensationHackDelta</c> — named aetheryte map-coord deltas (pure).
/// </summary>
public static class DistanceCompensation
{
	/// <summary>
	/// When <paramref name="enabled"/>, returns HTA parity deltas for known place names; otherwise (0,0).
	/// </summary>
	public static Vector2 GetDelta(string? aetherytePlaceName, bool enabled)
	{
		if (!enabled || string.IsNullOrEmpty(aetherytePlaceName))
			return Vector2.Zero;

		float x = 0f;
		float y = 0f;
		switch (aetherytePlaceName)
		{
			case "Tertium":
				y -= 5f;
				break;
			case "Base Omicron":
				x += 5f;
				break;
			case "Bestways Burrow":
				y -= 3f;
				x -= 2f;
				break;
			case "The Great Work":
				y -= 2f;
				break;
			case "The Macarenses Angle":
				y += 999f;
				break;
		}

		return new Vector2(x, y);
	}
}
