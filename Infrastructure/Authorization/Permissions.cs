namespace MongoApi.Infrastructure.Authorization;

public static class Permissions
{
    /// <summary>Admin роль в организации — добавление/удаление участников</summary>
    public const string OrgAdmin = "org.admin";

    /// <summary>Любой участник организации — чтение данных</summary>
    public const string OrgMember = "org.member";
}
