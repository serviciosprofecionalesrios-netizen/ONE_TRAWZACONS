using System.ComponentModel.DataAnnotations;
using ITServiceDeskApp.Models;

namespace ITServiceDeskApp.ViewModels.Users
{
    public class UserEditViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(120)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Department { get; set; } = string.Empty;

        [Required]
        public UserRole Role { get; set; }

        public bool IsActive { get; set; }
    }
}