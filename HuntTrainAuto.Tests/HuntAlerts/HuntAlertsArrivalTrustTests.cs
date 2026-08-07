#nullable enable

using HuntTrainAuto.Domain;

namespace HuntTrainAuto.Tests.HuntAlerts;

public sealed class HuntAlertsArrivalTrustTests
{
	[Fact]
	public void ClearUntrustedArrival_clears_ipc_aetheryte_and_returns_instance()
	{
		var flag = HuntFlag.FromMapLink(813, 1, 1000, 2000, "Test");
		flag.Arrival = ArrivalData.CreateOrNull(99u, 813u, 3, "Phoenix");

		var instance = HuntAlertsArrivalTrust.ClearUntrustedArrival(flag);

		Assert.Equal(3, instance);
		Assert.Null(flag.Arrival);
	}

	[Fact]
	public void ClearUntrustedArrival_returns_zero_when_no_arrival()
	{
		var flag = HuntFlag.FromMapLink(813, 1, 1000, 2000, "Test");

		Assert.Equal(0, HuntAlertsArrivalTrust.ClearUntrustedArrival(flag));
		Assert.Null(flag.Arrival);
	}

	[Fact]
	public void ClearUntrustedArrival_returns_zero_when_instance_unspecified()
	{
		var flag = HuntFlag.FromMapLink(813, 1, 1000, 2000, "Test");
		flag.Arrival = ArrivalData.CreateOrNull(42u, 813u, 0, "Phoenix");

		Assert.Equal(0, HuntAlertsArrivalTrust.ClearUntrustedArrival(flag));
		Assert.Null(flag.Arrival);
	}
}
