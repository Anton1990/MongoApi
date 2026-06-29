namespace MongoApi.GraphQL;

public record CreateOrganizationInput(string Name, string? Description = null);
