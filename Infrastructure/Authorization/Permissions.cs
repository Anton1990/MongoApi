namespace MongoApi.Infrastructure.Authorization;

public static class Permissions
{
    // Продукт
    public const string DeleteProduct = "product:delete";

    // Организация
    public const string RemoveMember = "org:members:remove";
}
