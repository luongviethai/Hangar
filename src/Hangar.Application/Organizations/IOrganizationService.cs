using Hangar.Application.Organizations.Dtos;

namespace Hangar.Application.Organizations;

public interface IOrganizationService
{
    Task<CreateOrganizationResult> CreateAsync(CreateOrganizationRequest request, int creatorUserId);
    Task<IReadOnlyList<OrganizationDto>> GetMyOrganizationsAsync(int userId);

    // null = Organization không tồn tại (404) — route đã qua OrgOwner policy nên hiếm xảy ra,
    // vẫn giữ để nhất quán quy ước null=404 của toàn project.
    Task<InviteMemberResult?> InviteMemberAsync(int organizationId, InviteMemberRequest request);

    // Không nhận organizationId — token tự xác định Organization nào (TokenHash unique toàn
    // cục), người được mời chỉ cần biết đúng 1 token từ email, không cần biết Id Organization.
    Task<AcceptInviteResult> AcceptInviteAsync(string token, int currentUserId, string currentUserEmail);
}
