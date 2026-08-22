namespace TradingScanner.Domain.Enums;

public enum MomentumState { Dormant, Building, Accelerating, Strong, Extended, Fading }
public enum SetupState { Waiting, FirstPullback, VwapHold, VwapReclaim, FifteenMinuteOrb, PremarketHighBreak, HighOfDayBreak, Extended, Failed }
public enum CatalystType { Fda, ClinicalTrial, Acquisition, Buyout, MajorContract, Earnings, Partnership, GovernmentContract, Patent, AnalystAction, CorporateUpdate, Offering, Dilution, ReverseSplit, Unknown, None }
public enum ScoreGrade { Ignore, B, A, APlus }
public enum MarketSession { Closed, Premarket, OpeningRange, Regular, AfterHours }
public enum AlertType { APlusDetected, MomentumAcceleration, VwapReclaim, VwapHold, Orb15Breakout, PremarketHighBreak, HodBreak, FirstPullback, CatalystDetected, HaltDetected }
