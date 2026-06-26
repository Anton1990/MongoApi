namespace MongoApi.Models.Dtos;

public class TokenResponse
{
    public string Token        { get; set; } = null!;
    public string TokenType    { get; set; } = "bearer";
    public string RefreshToken { get; set; } = null!;
    public string UserId       { get; set; } = null!;
}
