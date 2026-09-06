using System;
using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.Models
{
    public class HsDocument
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(160)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(80)]
        public string DocumentType { get; set; } = "Procedimiento";

        [Required]
        [MaxLength(40)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(30)]
        public string Version { get; set; } = "1.0";

        [MaxLength(100)]
        public string Site { get; set; } = "General";

        public DateTime EffectiveDateUtc { get; set; } = DateTime.UtcNow.Date;

        public DateTime? ReviewDateUtc { get; set; }

        [MaxLength(40)]
        public string Status { get; set; } = "Vigente";

        [MaxLength(500)]
        public string? FilePath { get; set; }

        [MaxLength(120)]
        public string CreatedBy { get; set; } = "Sistema";

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
