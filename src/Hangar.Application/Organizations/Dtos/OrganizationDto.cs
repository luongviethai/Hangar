namespace Hangar.Application.Organizations.Dtos;

// MyRole = vai trò của user ĐANG GỌI API trong chính Organization này — khác hẳn CRM, vì
// "role" không còn là thuộc tính cố định của user nữa (xem lý thuyết Ngày 1).
public record OrganizationDto(int Id, string Name, string Slug, string MyRole);
