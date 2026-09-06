using System;
using System.ComponentModel.DataAnnotations;

namespace ITServiceDeskApp.Models
{
    public class FinanceAttachment
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(40)]
        public string DocumentType { get; set; } = string.Empty;

        public int DocumentId { get; set; }

        [Required]
        [MaxLength(260)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string StoredPath { get; set; } = string.Empty;

        [MaxLength(120)]
        public string? ContentType { get; set; }

        public long SizeBytes { get; set; }

        [MaxLength(120)]
        public string UploadedBy { get; set; } = "Sistema";

        public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
