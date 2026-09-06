using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.Models
{
    public enum PriorityLevel
    {
        [Display(Name = "Baja")]
        Low = 1,

        [Display(Name = "Media")]
        Medium = 2,

        [Display(Name = "Alta")]
        High = 3,

        [Display(Name = "Critica")]
        Critical = 4
    }

    public enum TicketStatus
    {
        [Display(Name = "Abierto")]
        Open = 1,

        [Display(Name = "En Progreso")]
        InProgress = 2,

        [Display(Name = "Resuelto")]
        Resolved = 3,

        [Display(Name = "Cerrado")]
        Closed = 4
    }

    public enum UserRole
    {
        [Display(Name = "Administrador")]
        Administrator = 1,

        [Display(Name = "Tecnico IT")]
        Technician = 2,

        [Display(Name = "Usuario Final")]
        EndUser = 3,

        [Display(Name = "Coordinador IT")]
        CoordinadorIT = 4,

        [Display(Name = "Gerencia General")]
        GerenciaGeneral = 5,

        [Display(Name = "Gestor H&S")]
        GestorHS = 6,

        [Display(Name = "Gerente H&S")]
        GerenteHS = 7,

        [Display(Name = "Supervisor H&S")]
        SupervisorHS = 8
    }
}

