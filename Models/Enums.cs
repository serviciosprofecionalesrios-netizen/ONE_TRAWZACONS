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
        SupervisorHS = 8,

        [Display(Name = "Gerente Taller")]
        GerenteTaller = 9,

        [Display(Name = "Supervisor Taller")]
        SupervisorTaller = 10,

        [Display(Name = "Gerente Compras")]
        GerenteCompras = 11,

        [Display(Name = "CEO")]
        CEO = 12,

        [Display(Name = "Supervisor Logística")]
        SupervisorLogistica = 13,

        [Display(Name = "Encargado de Almacén PCT")]
        EncargadoAlmacenPCT = 14,

        [Display(Name = "Encargado de Almacén PSB")]
        EncargadoAlmacenPSB = 15,

        [Display(Name = "Supervisor General PSB")]
        SupervisorGeneralPSB = 16,

        [Display(Name = "Supervisor Encargado ML")]
        SupervisorEncargadoML = 17,

        [Display(Name = "Supervisor ML")]
        SupervisorML = 18,

        [Display(Name = "Supervisor Encargado MLL")]
        SupervisorEncargadoMLL = 19,

        [Display(Name = "Supervisor MLL")]
        SupervisorMLL = 20,

        [Display(Name = "Monitoreo")]
        Monitoreo = 21
    }
}

