#nullable enable

using System.Numerics;

namespace HuntTrainAuto.Windows;

/// <summary>HuntAlerts NotifyWindow color tokens (ABGR ImGui packed).</summary>
internal static class AlertTheme
{
	public static readonly uint Accent = 0xFFF2B36D;
	public static readonly uint Subtle = 0xFF888888;
	public static readonly uint Text = 0xFFE0E0E0;

	public static readonly uint SRankBg = 0xFF1E1E5C;
	public static readonly uint SRankBorder = 0xFF4040A0;
	public static readonly uint SRankText = 0xFF8A8AFF;

	public static readonly uint TrainBg = 0xFF5C3A1E;
	public static readonly uint TrainBorder = 0xFFC08040;
	public static readonly uint TrainText = 0xFFFFC68A;

	public static readonly uint KindBg = 0xFF2A2A2A;
	public static readonly uint KindBorder = 0xFF4A4A4A;
	public static readonly uint KindText = 0xFFB8B8B8;

	public static readonly uint WorldBg = 0xFF1E3A1A;
	public static readonly uint WorldBorder = 0xFF508040;
	public static readonly uint WorldText = 0xFF90D080;

	public static readonly uint InfoBtn = 0xFF601BD8;
	public static readonly uint InfoBtnHover = 0xFF804BF8;
	public static readonly uint InfoBtnActive = 0xFF4000A8;

	public static readonly uint SuccessBtn = 0xFF3CA63C;
	public static readonly uint SuccessBtnHover = 0xFF5CC65C;
	public static readonly uint SuccessBtnActive = 0xFF2C862C;

	public static Vector2 BadgePadding => new(6, 1);
}

internal enum AlertBadgeStyle
{
	SRank,
	Train,
	Kind,
	World,
}

internal enum AlertButtonRole
{
	Info,
	Success,
}
