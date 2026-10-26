using System.Security.Cryptography;
using System.Text;
using Hangar.Application.Common.Interfaces;
using Hangar.Application.Organizations.Dtos;
using Hangar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hangar.Application.Organizations;

public class OrganizationService : IOrganizationService
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailSender _emailSender;

    public OrganizationService(IApplicationDbContext context, IEmailSender emailSender)
    {
        _context = context;
        _emailSender = emailSender;
    }

    public async Task<CreateOrganizationResult> CreateAsync(CreateOrganizationRequest request, int creatorUserId)
    {
        var slugTaken = await _context.Organizations.AnyAsync(o => o.Slug == request.Slug);
        if (slugTaken)
        {
            return new CreateOrganizationResult(false, null, $"Slug '{request.Slug}' đã được dùng.");
        }

        var organization = new Organization
        {
            Name = request.Name,
            Slug = request.Slug,
            CreatedAtUtc = DateTime.UtcNow
        };

        // Người tạo tự thành Owner — thêm qua navigation property (không gán OrganizationId tay)
        // để EF Core tự fixup FK đúng khi SaveChangesAsync, không cần 2 lần SaveChanges như
        // CreateOrder ở CRM (ở đó cần Order.Id có trước để gán StockMovement.Reference — ở đây
        // không có "con" nào cần biết Id của Organization trước khi lưu).
        organization.Members.Add(new OrganizationMember
        {
            UserId = creatorUserId,
            Role = OrganizationRole.Owner,
            JoinedAtUtc = DateTime.UtcNow
        });

        _context.Organizations.Add(organization);
        await _context.SaveChangesAsync();

        return new CreateOrganizationResult(true, organization.ToDto(OrganizationRole.Owner), null);
    }

    public async Task<IReadOnlyList<OrganizationDto>> GetMyOrganizationsAsync(int userId)
    {
        var memberships = await _context.OrganizationMembers
            .Where(m => m.UserId == userId)
            .Include(m => m.Organization)
            .ToListAsync();

        return memberships.Select(m => m.Organization.ToDto(m.Role)).ToList();
    }

    public async Task<InviteMemberResult?> InviteMemberAsync(int organizationId, InviteMemberRequest request)
    {
        var organization = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId);
        if (organization is null)
        {
            return null;
        }

        var plainToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var invite = new OrganizationInvite
        {
            OrganizationId = organizationId,
            Email = request.Email,
            Role = request.Role,
            TokenHash = Hash(plainToken),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        };

        _context.OrganizationInvites.Add(invite);
        await _context.SaveChangesAsync();

        // Token CHỈ đi qua email — KHÔNG trả về trong response API, vì người gọi API này
        // (Owner đang mời) không phải người được phép dùng token (người được mời mới được).
        var body =
            $"Bạn được mời tham gia Organization '{organization.Name}' với vai trò {request.Role}.\n\n" +
            $"Dùng token sau để accept (POST /api/organizations/invites/accept):\n{plainToken}\n\n" +
            $"Token hết hạn sau 7 ngày.";

        await _emailSender.SendAsync(request.Email, $"Lời mời tham gia {organization.Name} trên Hangar", body);

        return new InviteMemberResult(true, null);
    }

    public async Task<AcceptInviteResult> AcceptInviteAsync(string token, int currentUserId, string currentUserEmail)
    {
        var hash = Hash(token);
        var invite = await _context.OrganizationInvites.FirstOrDefaultAsync(i => i.TokenHash == hash);

        if (invite is null)
        {
            return new AcceptInviteResult(false, null, "Token không hợp lệ.");
        }

        if (invite.AcceptedAtUtc is not null)
        {
            return new AcceptInviteResult(false, null, "Lời mời này đã được accept trước đó.");
        }

        if (invite.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return new AcceptInviteResult(false, null, "Lời mời đã hết hạn.");
        }

        if (!string.Equals(invite.Email, currentUserEmail, StringComparison.OrdinalIgnoreCase))
        {
            return new AcceptInviteResult(false, null, "Lời mời này gửi cho email khác, không phải tài khoản đang đăng nhập.");
        }

        var alreadyMember = await _context.OrganizationMembers
            .AnyAsync(m => m.OrganizationId == invite.OrganizationId && m.UserId == currentUserId);

        if (alreadyMember)
        {
            return new AcceptInviteResult(false, null, "Bạn đã là thành viên Organization này.");
        }

        invite.AcceptedAtUtc = DateTime.UtcNow;

        _context.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = invite.OrganizationId,
            UserId = currentUserId,
            Role = invite.Role,
            JoinedAtUtc = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        var organization = await _context.Organizations.FirstAsync(o => o.Id == invite.OrganizationId);

        return new AcceptInviteResult(true, organization.ToDto(invite.Role), null);
    }

    // SHA-256 — token là random 256-bit do mình tự sinh, không phải thứ con người nghĩ ra, nên
    // không cần thuật toán chậm có salt như password (giống lý do RefreshToken dùng SHA-256).
    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
