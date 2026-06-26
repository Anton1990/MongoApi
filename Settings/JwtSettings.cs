namespace MongoApi.Settings;

public class JwtSettings
{
    /// <summary>Симметричный ключ подписи (минимум 32 символа).</summary>
    public string SecretKey { get; set; } = null!;

    /// <summary>Жизнь access token в секундах (по умолчанию 24 часа).</summary>
    public int TokenLifetime { get; set; } = 86400;

    /// <summary>Жизнь refresh token в секундах (по умолчанию 30 дней).</summary>
    public int RefreshTokenLifetime { get; set; } = 2592000;

    public string Issuer { get; set; } = "MongoApi";
    public string TokenType { get; set; } = "bearer";
}
