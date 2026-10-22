using ITServiceDeskApp.Models;
namespace ITServiceDeskApp.ViewModels;
public sealed class SiteActivityViewModel
{
    public DateTime GeneratedAtUtc {get;set;}
    public DateTime From {get;set;}
    public DateTime To {get;set;}
    public int? UserId {get;set;}
    public int Online {get;set;}
    public int TrackedUsers {get;set;}
    public long TotalActiveSeconds {get;set;}
    public int EventCount {get;set;}
    public List<SiteActivityUserRow> Users {get;set;}=[];
    public List<SiteActivityEvent> Events {get;set;}=[];
    public List<SiteActivityUserChoice> Choices {get;set;}=[];
    public InventoryTablePage UserPage {get;set;}=new(0,1,25);
    public InventoryTablePage EventPage {get;set;}=new(0,1,50);
    public static string Local(DateTime? date)=>date?.AddHours(-6).ToString("dd/MM/yyyy HH:mm:ss") ?? "Sin registro";
    public static string Duration(long seconds)=>$"{seconds/3600:N0} h {(seconds%3600)/60:00} min";
}
public sealed class SiteActivityUserRow
{
    public int Id {get;set;}
    public string Name {get;set;}="";
    public string Email {get;set;}="";
    public string Status {get;set;}="Sin registro";
    public DateTime? LastLogin {get;set;}
    public DateTime? LastSeen {get;set;}
    public DateTime? LastActivity {get;set;}
    public string Page {get;set;}="—";
    public long ActiveSeconds {get;set;}
    public int Sessions {get;set;}
}
public sealed record SiteActivityUserChoice(int Id,string Name,string Email);
