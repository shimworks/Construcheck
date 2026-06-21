namespace Construcheck.API.Modules.Auth.DTOs;

public record UpdateUserRolesRequest(List<Guid> RoleIds);