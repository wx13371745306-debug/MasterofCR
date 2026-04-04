public enum DayCyclePhase
{
    /// <summary>开局：无计时、不生成客人，可商店订货，第一天准备时统一交货。</summary>
    DayZero,
    Prep,
    Business,
    ExtendedBusiness,
    Closing,
    NextDayTransition
}
