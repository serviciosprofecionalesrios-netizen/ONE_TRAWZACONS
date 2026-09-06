using System;
using System.Collections.Generic;
using ITServiceDeskApp.Models;

namespace ITServiceDeskApp.ViewModels.Users
{
    public sealed class UserListViewModel
    {
        public string? Search { get; set; }
        public string SelectedRole { get; set; } = "all";
        public string SelectedDepartment { get; set; } = "all";
        public string SelectedStatus { get; set; } = "all";
        public string SortBy { get; set; } = "created";
        public string SortDir { get; set; } = "desc";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalItems { get; set; }
        public int TotalPages { get; set; } = 1;
        public string? Message { get; set; }
        public string? Error { get; set; }

        public List<UserListOptionViewModel> RoleOptions { get; set; } = new();
        public List<UserListOptionViewModel> DepartmentOptions { get; set; } = new();
        public List<UserListOptionViewModel> StatusOptions { get; set; } = new();
        public List<UserListItemViewModel> Users { get; set; } = new();

        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
    }

    public sealed class UserListItemViewModel
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class UserListOptionViewModel
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }
}
