using System;

namespace CS2DataExport;

public static class TransportCongestionMetrics
{
  /// <summary>
  /// Matches StuckMovingObjectSystem: blockers at or above this speed are not treated as stuck.
  /// </summary>
  public const byte SlowBlockerMaxSpeedThreshold = 6;

  public static double? ComputeCongestionIndex(int slowBlockedVehicles, int roadVehicleEntities)
  {
    if (roadVehicleEntities <= 0)
    {
      return null;
    }

    double rawRatio = slowBlockedVehicles / (double)roadVehicleEntities;
    if (rawRatio < 0)
    {
      rawRatio = 0;
    }
    else if (rawRatio > 1)
    {
      rawRatio = 1;
    }

    return Math.Round(rawRatio, 4, MidpointRounding.AwayFromZero);
  }
}
