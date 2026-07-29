using Construcheck.Auth.Domain;

namespace Construcheck.Auth.Application.DTOs;

public record UpdateUserRolesRequest(List<RoleType> Roles);
