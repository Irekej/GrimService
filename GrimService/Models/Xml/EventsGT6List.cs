using System;
using System.Collections.Generic;
using System.Runtime.ConstrainedExecution;
using System.Xml.Serialization;

namespace GrimService.Models.Xml
{
    [XmlRoot("xml")]
    public class EventsGT6List
    {
        [XmlElement("GameParameter")]
        public EventsGT6GameParameter GameParameter { get; set; }
    }

    public class EventsGT6GameParameter
    {
        [XmlAttribute("version")]
        public int Version { get; set; }

        [XmlElement("championship")]
        public int Championship { get; set; }

        [XmlElement("folder_id")]
        public int FolderId { get; set; }

        [XmlElement("event_id_list")]
        public int EventIdList { get; set; }

        [XmlElement("events")]
        public EventsGT6Events Events { get; set; }
    }

    public class EventsGT6Events
    {
        [XmlElement("event")]
        public EventsGT6Event Event { get; set; }
    }

    public class EventsGT6Event
    {
        [XmlElement("constraint")]
        public EventsGT6Constraint Constraint { get; set; }

        [XmlElement("entry_set")]
        public EventsGT6EntrySet EntrySet { get; set; }

        [XmlAttribute("event_id")]
        public int EventId { get; set; }

        [XmlAttribute("event_type")]
        public string EventType { get; set; }

        [XmlAttribute("game_mode")]
        public string GameMode { get; set; }

        [XmlElement("information")]
        public EventsGT6Information Information { get; set; }

        [XmlAttribute("inheritance")]
        public string Inheritance { get; set; }

        [XmlElement("race")]
        public EventsGT6Race Race { get; set; }
    }

    public class EventsGT6Constraint
    {
        [XmlElement("abs")]
        public int Abs { get; set; }

        [XmlElement("active_steering")]
        public int ActiveSteering { get; set; }

        [XmlElement("asm")]
        public int Asm { get; set; }

        [XmlElement("drift_type")]
        public int DriftType { get; set; }

        [XmlElement("driving_line")]
        public int DrivingLine { get; set; }

        [XmlElement("enemy_tire")]
        public int EnemyTire { get; set; }

        [XmlElement("limit_tire_f")]
        public int LimitTireFront { get; set; }

        [XmlElement("limit_tire_r")]
        public int LimitTireRear { get; set; }

        [XmlElement("need_tire_f")]
        public int NeedTireFront { get; set; }

        [XmlElement("need_tire_r")]
        public int NeedTireRear { get; set; }

        [XmlElement("simulation")]
        public int Simulation { get; set; }

        [XmlElement("suggest_tire_f")]
        public int SuggestTireFront { get; set; }

        [XmlElement("suggest_tire_r")]
        public int SuggestTireRear { get; set; }

        [XmlElement("tcs")]
        public int Tcs { get; set; }

        [XmlElement("transmission")]
        public int Transmission { get; set; }
    }

    public class EventsGT6EntrySet
    {
        [XmlElement("entry")]
        public EventsGT6Entry Entry { get; set; }
    }

    public class EventsGT6Entry
    {
        [XmlElement("car")]
        public EventsGT6Car Car { get; set; }

        [XmlElement("delay")]
        public int Delay { get; set; }

        [XmlElement("driver_name")]
        public string DriverName { get; set; }

        [XmlElement("player_no")]
        public int PlayerNo { get; set; }

        [XmlElement("race_class_id")]
        public int RaceClassId { get; set; }



    }

    public class EventsGT6Car
    {
        [XmlAttribute("color")]
        public int Color { get; set; }

        [XmlAttribute("label")]
        public string Label { get; set; }
    }

    public class EventsGT6Information
    {
        [XmlAttribute("advanced_notice")]
        public string AdvancedNotice { get; set; }

        [XmlElement("flier_other_info")]
        public string FlierOtherInfo { get; set; }

        [XmlElement("logo_other_info")]
        public string LogoOtherInfo { get; set; }

        [XmlElement("race_info_minute")]
        public string RaceInfoMinute { get; set; }

        [XmlElement("title")]
        public EventsGT6Title Title { get; set; }

        [XmlElement("one_line_title")]
        public EventsGT6OneLine OneLineTitle { get; set; }

        [XmlElement("description")]
        public EventsGT6Desc Description { get; set; }
    }

    public class EventsGT6Title
    {
        [XmlElement("JP")]
        public string JP { get; set; }

        [XmlElement("US")]
        public string US { get; set; }

        [XmlElement("GB")]
        public string GB { get; set; }

        [XmlElement("FR")]
        public string FR { get; set; }

        [XmlElement("ES")]
        public string ES { get; set; }

        [XmlElement("DE")]
        public string DE { get; set; }

        [XmlElement("IT")]
        public string IT { get; set; }

        [XmlElement("NL")]
        public string NL { get; set; }

        [XmlElement("PT")]
        public string PT { get; set; }

        [XmlElement("RU")]
        public string RU { get; set; }

        [XmlElement("PL")]
        public string PL { get; set; }

        [XmlElement("TR")]
        public string TR { get; set; }

        [XmlElement("KR")]
        public string KR { get; set; }

        [XmlElement("EL")]
        public string EL { get; set; }

        [XmlElement("FI")]
        public string FI { get; set; }

        [XmlElement("CZ")]
        public string CZ { get; set; }

        [XmlElement("HU")]
        public string HU { get; set; }

        [XmlElement("BP")]
        public string BP { get; set; }

        [XmlElement("MS")]
        public string MS { get; set; }
    }

    public class EventsGT6OneLine
    {
        [XmlElement("JP")]
        public string JP { get; set; }

        [XmlElement("US")]
        public string US { get; set; }

        [XmlElement("GB")]
        public string GB { get; set; }

        [XmlElement("FR")]
        public string FR { get; set; }

        [XmlElement("ES")]
        public string ES { get; set; }

        [XmlElement("DE")]
        public string DE { get; set; }

        [XmlElement("IT")]
        public string IT { get; set; }

        [XmlElement("NL")]
        public string NL { get; set; }

        [XmlElement("PT")]
        public string PT { get; set; }

        [XmlElement("RU")]
        public string RU { get; set; }

        [XmlElement("PL")]
        public string PL { get; set; }

        [XmlElement("TR")]
        public string TR { get; set; }

        [XmlElement("KR")]
        public string KR { get; set; }

        [XmlElement("EL")]
        public string EL { get; set; }

        [XmlElement("FI")]
        public string FI { get; set; }

        [XmlElement("CZ")]
        public string CZ { get; set; }

        [XmlElement("HU")]
        public string HU { get; set; }

        [XmlElement("BP")]
        public string BP { get; set; }

        [XmlElement("MS")]
        public string MS { get; set; }
    }

    public class EventsGT6Desc
    {
        [XmlElement("JP")]
        public string JP { get; set; }

        [XmlElement("US")]
        public string US { get; set; }

        [XmlElement("GB")]
        public string GB { get; set; }

        [XmlElement("FR")]
        public string FR { get; set; }

        [XmlElement("ES")]
        public string ES { get; set; }

        [XmlElement("DE")]
        public string DE { get; set; }

        [XmlElement("IT")]
        public string IT { get; set; }

        [XmlElement("NL")]
        public string NL { get; set; }

        [XmlElement("PT")]
        public string PT { get; set; }

        [XmlElement("RU")]
        public string RU { get; set; }

        [XmlElement("PL")]
        public string PL { get; set; }

        [XmlElement("TR")]
        public string TR { get; set; }

        [XmlElement("KR")]
        public string KR { get; set; }

        [XmlElement("EL")]
        public string EL { get; set; }

        [XmlElement("FI")]
        public string FI { get; set; }

        [XmlElement("CZ")]
        public string CZ { get; set; }

        [XmlElement("HU")]
        public string HU { get; set; }

        [XmlElement("BP")]
        public string BP { get; set; }

        [XmlElement("MS")]
        public string MS { get; set; }
    }

    public class EventsGT6Race
    {
        [XmlAttribute("academy_event")]
        public int AcademyEvent { get; set; }

        [XmlAttribute("accumulation")]
        public int Accumulation { get; set; }

        [XmlAttribute("allow_codriver")]
        public int AllowCodriver { get; set; }

        [XmlAttribute("auto_standing_delay")]
        public int AutoStandingDelay { get; set; }

        [XmlAttribute("autostart_pitout")]
        public int AutostartPitout { get; set; }

        [XmlAttribute("behavior_damage_type")]
        public string BehaviorDamageType { get; set; }

        [XmlAttribute("behavior_slip_stream_type")]
        public string BehaviorSlipStreamType { get; set; }

        [XmlAttribute("boost_type")]
        public int BoostType { get; set; }

        [XmlAttribute("bspec_vitality10")]
        public int BSpecVitality10 { get; set; }

        [XmlAttribute("complete_type")]
        public string CompleteType { get; set; }

        [XmlAttribute("consume_fuel")]
        public int ConsumeFuel { get; set; }

        [XmlAttribute("consume_tire")]
        public int ConsumeTire { get; set; }

        [XmlAttribute("datetime")]
        public string Datetime { get; set; }

        [XmlAttribute("decisive_weather")]
        public string DecisiveWeather { get; set; }

        [XmlAttribute("disable_collision")]
        public int DisableCollision { get; set; }

        [XmlAttribute("disable_recording_replay")]
        public int DisableRecordingReplay { get; set; }

        [XmlAttribute("enable_damage")]
        public int EnableDamage { get; set; }

        [XmlAttribute("enable_pit")]
        public int EnablePit { get; set; }

        [XmlAttribute("endless")]
        public int Endless { get; set; }

        [XmlAttribute("event_goal_v")]
        public int EventGoalV { get; set; }

        [XmlAttribute("event_goal_width")]
        public int EventGoalWidth { get; set; }

        [XmlAttribute("finish_type")]
        public string FinishType { get; set; }

        [XmlAttribute("fixed_retention")]
        public int FixedRetention { get; set; }

        [XmlAttribute("flagset")]
        public string Flagset { get; set; }

        [XmlAttribute("ghost_presence_type")]
        public string GhostPresenceType { get; set; }

        [XmlAttribute("ghost_type")]
        public string GhostType { get; set; }

        [XmlAttribute("grid_sort_type")]
        public string GridSortType { get; set; }

        [XmlAttribute("immediate_finish")]
        public int ImmediateFinish { get; set; }

        [XmlAttribute("initial_retention10")]
        public int InitialRetention10 { get; set; }

        [XmlAttribute("keep_load_ghost")]
        public int KeepLoadGhost { get; set; }

        [XmlAttribute("lighting_mode")]
        public string LightingMode { get; set; }

        [XmlAttribute("line_ghost_record_type")]
        public string LineGhostRecordType { get; set; }

        [XmlAttribute("line_ghost_play_max")]
        public int LineGhostPlayMax { get; set; }

        [XmlAttribute("low_mu_type")]
        public string LowMuType { get; set; }

        [XmlAttribute("mu_ratio100")]
        public int MuRatio100 { get; set; }

        [XmlAttribute("online_on")]
        public int OnlineOn { get; set; }

        [XmlAttribute("pace_note")]
        public int PaceNote { get; set; }

        [XmlAttribute("penalty_level")]
        public int PenaltyLevel { get; set; }

        [XmlAttribute("penalty_no_reset")]
        public int PenaltyNoReset { get; set; }

        [XmlAttribute("race_limit_laps")]
        public int RaceLimitLaps { get; set; }

        [XmlAttribute("race_limit_minute")]
        public int RaceLimitMinute { get; set; }

        [XmlAttribute("race_type")]
        public string RaceType { get; set; }

        [XmlAttribute("start_type")]
        public string StartType { get; set; }

        [XmlAttribute("time_progress_speed")]
        public int TimeProgressSpeed { get; set; }

        [XmlAttribute("time_to_finish")]
        public int TimeToFinish { get; set; }

        [XmlAttribute("time_to_start")]
        public int TimeToStart { get; set; }

        [XmlAttribute("weather_base_celsius")]
        public int WeatherBaseCelsius { get; set; }

        [XmlAttribute("weather_max_celsius")]
        public int WeatherMaxCelsius { get; set; }

        [XmlAttribute("weather_min_celsius")]
        public int WeatherMinCelsius { get; set; }

        [XmlAttribute("weather_no_precipitation")]
        public int WeatherNoPrecipitation { get; set; }

        [XmlAttribute("weather_no_schedule")]
        public int WeatherNoSchedule { get; set; }

        [XmlAttribute("weather_no_wind")]
        public int WeatherNoWind { get; set; }

        [XmlAttribute("weather_point_num")]
        public int WeatherPointNum { get; set; }

        [XmlAttribute("weather_prec_rain_only")]
        public int WeatherPrecRainOnly { get; set; }

        [XmlAttribute("weather_prec_snow_only")]
        public int WeatherPrecSnowOnly { get; set; }

        [XmlAttribute("weather_random")]
        public int WeatherRandom { get; set; }

        [XmlAttribute("weather_random_seed")]
        public int WeatherRandomSeed { get; set; }

        [XmlAttribute("weather_total_sec")]
        public int WeatherTotalSec { get; set; }

        [XmlAttribute("over_entry_max")]
        public int OverEntryMax { get; set; }

        [XmlAttribute("with_ghost")]
        public int WithGhost { get; set; }

        [XmlAttribute("replace_at_courseout")]
        public int ReplaceAtCourseout { get; set; }

        [XmlAttribute("weather_accel10")]
        public int WeatherAccel10 { get; set; }

        [XmlAttribute("weather_accel_water_retention10")]
        public int WeatherAccelWaterRetention10 { get; set; }

        [XmlAttribute("boost_flag")]
        public int BoostFlag { get; set; }

        [XmlAttribute("entry_max")]
        public int EntryMax { get; set; }

        [XmlAttribute("racers_max")]
        public int RacersMax { get; set; }
    }

    public class EventsGT6Reward
    {
        [XmlAttribute("is_once")]
        public int IsOnce { get; set; }

        [XmlAttribute("percent_at_pp100")]
        public int PercentAtPp100 { get; set; }

        [XmlAttribute("pp_base")]
        public int PpBase { get; set; }

        [XmlAttribute("present_type")]
        public string PresentType { get; set; }

        [XmlAttribute("prize_type")]
        public int PrizeType { get; set; }

        [XmlAttribute("special_reward_code")]
        public int SpecialRewardCode { get; set; }

        [XmlElement("point_table")]
        public string PointTable { get; set; }

        [XmlElement("prize_table")]
        public EventsGT6PrizeTable PrizeTable { get; set; }

        [XmlElement("star_table")]
        public string StarTable { get; set; }

        [XmlElement("present_type")]
        public string PresentType2 { get; set; }

        [XmlElement("present")]
        public string Present { get; set; }
    }

    public class EventsGT6PrizeTable
    {
        [XmlElement("prize")]
        public List<int> Prize { get; set; }
    }

}
