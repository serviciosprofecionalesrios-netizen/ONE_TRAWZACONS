using System;
using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.ViewModels.Tickets
{
    public sealed class ArticleOutputCreateViewModel
    {
        [Required]
        [DataType(DataType.DateTime)]
        [Display(Name = "Fecha/Hora")]
        public DateTime DispatchDateTime { get; set; } = DateTime.Now;

        [Required]
        [MaxLength(80)]
        [Display(Name = "N° Requisa")]
        public string RequisitionNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        [Display(Name = "Equipo destino")]
        public string TargetEquipment { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Display(Name = "Almacen despacho")]
        public string DispatchWarehouse { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        [Display(Name = "Recibi")]
        public string ReceivedBy { get; set; } = string.Empty;

        [Required]
        [MaxLength(40)]
        [Display(Name = "Estado")]
        public string DispatchStatus { get; set; } = "PENDIENTE";

        [MaxLength(160)]
        [Display(Name = "Firma")]
        public string? SignatureName { get; set; }

        [MaxLength(180)]
        [Display(Name = "Usuario")]
        public string UserEmail { get; set; } = string.Empty;

        [MaxLength(1200)]
        [Display(Name = "Notas")]
        public string? AdditionalNotes { get; set; }

        public string SparePartsJson { get; set; } = "[]";
    }
}
