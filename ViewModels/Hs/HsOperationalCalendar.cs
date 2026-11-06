namespace ITServiceDeskApp.ViewModels.Hs;
public sealed class HsOperationalCalendar
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string View { get; set; } = "week";
    public DateTime Anchor { get; set; }
    public string Driver { get; set; } = "";
    public string Training { get; set; } = "";
    public string Status { get; set; } = "";
    public string Site { get; set; } = "";
    public bool FullDay { get; set; }
    public int ActiveDrivers { get; set; }
    public int Valid { get; set; }
    public int Due { get; set; }
    public int Expired { get; set; }
    public List<HsOperationalEvent> Events { get; set; } = [];
    public List<HsOperationalEvent> Alerts { get; set; } = [];
    public List<HsOperationalEvent> Expiries { get; set; } = [];
    public Dictionary<string,string> Drivers { get; set; } = [];
    public Dictionary<string,string> DriverCodes { get; set; } = [];
    public List<string> Trainings { get; set; } = [];
    public List<string> Sites { get; set; } = [];
    public string? SourceWarning { get; set; }
}
public sealed class HsOperationalEvent
{
    public string Key { get; set; } = "";
    public DateTime Date { get; set; }
    public bool AllDay { get; set; }
    public bool IsTraining { get; set; }
    public string Title { get; set; } = "";
    public string Group { get; set; } = "Otras actividades H&S";
    public string Driver { get; set; } = "";
    public string Code { get; set; } = "";
    public string DriverId { get; set; } = "";
    public string Site { get; set; } = "";
    public string Training { get; set; } = "";
    public string Performed { get; set; } = "";
    public int? Days { get; set; }
    public string Status { get; set; } = "Programada";
    public string StatusLabel => Status switch { "vigente" => "Vigente", "porvencer" => "Por vencer", "vencida" => "Vencida / hoy", _ => Status };
    public string UrgencyLabel => Days switch { null => StatusLabel, < 0 => $"Vencida hace {Math.Abs(Days.Value)} días", 0 => "Vence hoy", 1 => "Vence mañana", _ => $"Vence en {Days} días" };
    public string Tone { get; set; } = "primary";
    public string Url { get; set; } = "";
    public bool RedIndicator => Days <= 30;
}
