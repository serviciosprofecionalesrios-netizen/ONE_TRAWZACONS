using ITServiceDeskApp.Models;
namespace ITServiceDeskApp.Services;
public static class MaintenanceSemaphore
{
    public static (string Label,string Tone,int Rank) Evaluate(double remainingHours,TicketStatus status,string? stage)
    {
        if(status==TicketStatus.Closed || string.Equals(stage?.Trim(),"Cerrada",StringComparison.OrdinalIgnoreCase))return("Completada","secondary",3);
        if(remainingHours<0)return("Vencida","danger",0);
        if(remainingHours<=4)return("Por vencer","warning",1);
        if(remainingHours<=12)return("Seguimiento","info",2);
        return("En tiempo","success",2);
    }
}
