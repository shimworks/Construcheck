using Construcheck.API.Modules.Auth.Enums;

namespace Construcheck.API.Modules.Auth.DTOs;

public record UpdateUserRolesRequest(List<RoleType> Roles);