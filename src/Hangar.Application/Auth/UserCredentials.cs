namespace Hangar.Application.Auth;

// Khác CRM: không có field Role/Roles — JWT ở Hangar chỉ xác định danh tính (UserId/Email),
// không xác định quyền (quyền phụ thuộc Organization đang truy cập, tra ở tầng authorization).
public record UserCredentials(int UserId, string Email, string FullName);
